<#
    Script de humo para el flujo de inventario de Axon.
    Login -> crear categoria -> obtener unidad -> crear producto -> listar -> ajustar stock -> verificar.

    Requiere Windows PowerShell 5.1. Si POST /api/inventory/categories o
    GET /api/inventory/units todavia no existen en la API, hace fallback
    a psql dentro del contenedor de Postgres (docker exec).
#>

param(
    [string]$BaseUrl = "http://localhost:5026",
    [string]$TenantSlug = "ferreteria-don-pedro",
    [string]$Email = "admin@ferreteriadonpedro.com",
    [string]$Password = "Admin1234",
    [string]$PostgresContainer = "axon_postgres",
    [string]$PostgresUser = "axon_user",
    # OJO: appsettings.Development.json usa "axon_master" pero docker-compose.yml
    # crea "axon_db" -- si no se ha resuelto ese mismatch, pasa -DatabaseName axon_db.
    [string]$DatabaseName = "axon_master"
)

$ErrorActionPreference = "Stop"

$script:Token = $null
$script:SchemaName = $null
$script:CategoryId = $null
$script:UnitId = $null
$script:ProductId = $null

$script:Results = [ordered]@{
    Login       = $false
    Category    = $false
    Unit        = $false
    Product     = $false
    StockAdjust = $false
    FinalCheck  = $false
}

# ---------- Helpers ----------

function Get-AuthHeaders {
    return @{
        "X-Tenant-Slug" = $TenantSlug
        "Authorization" = "Bearer $script:Token"
    }
}

function Write-ErrorDetails {
    param(
        [Parameter(Mandatory)] $ErrorRecord,
        [Parameter(Mandatory)] [string]$Step
    )

    Write-Host "`n$([char]0x274C) ERROR en el paso: $Step" -ForegroundColor Red

    $exception = $ErrorRecord.Exception

    if ($exception.Response) {
        try {
            $statusCode = [int]$exception.Response.StatusCode
            Write-Host "Status Code: $statusCode" -ForegroundColor Red
        }
        catch {
            Write-Host "Status Code: (no disponible)" -ForegroundColor Red
        }

        try {
            $stream = $exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $body = $reader.ReadToEnd()
            $reader.Close()
            Write-Host "Response Body:" -ForegroundColor Red
            Write-Host $body -ForegroundColor Red
        }
        catch {
            Write-Host "No se pudo leer el cuerpo de la respuesta de error." -ForegroundColor Red
        }
    }
    else {
        Write-Host "Excepcion: $($exception.Message)" -ForegroundColor Red
    }
}

function Get-StatusCode {
    param($ErrorRecord)

    if ($ErrorRecord.Exception.Response) {
        try { return [int]$ErrorRecord.Exception.Response.StatusCode } catch { return $null }
    }
    return $null
}

function Invoke-Psql {
    param([Parameter(Mandatory)] [string]$Sql)

    $output = docker exec -i $PostgresContainer psql -U $PostgresUser -d $DatabaseName -t -A -c $Sql 2>&1

    if ($LASTEXITCODE -ne 0) {
        throw "psql fallo (exit $LASTEXITCODE) contra la BD '$DatabaseName': $output"
    }

    return ($output | Out-String).Trim()
}

function Get-TenantSchemaName {
    if ($script:SchemaName) { return $script:SchemaName }

    $sql = "SELECT schema_name FROM public.tenants WHERE slug = '$TenantSlug';"
    $result = Invoke-Psql -Sql $sql

    if ([string]::IsNullOrWhiteSpace($result)) {
        throw "No se encontro el tenant '$TenantSlug' en public.tenants (BD: $DatabaseName)."
    }

    $script:SchemaName = $result
    Write-Host "   (schema del tenant: $script:SchemaName)" -ForegroundColor DarkGray
    return $script:SchemaName
}

function Get-StatusIcon {
    param([bool]$Success)
    if ($Success) { return [char]0x2705 } else { return [char]0x274C }
}

function Show-Summary {
    Write-Host "`n=== RESUMEN ===" -ForegroundColor Cyan
    Write-Host "$(Get-StatusIcon $script:Results.Login) Login"
    Write-Host "$(Get-StatusIcon $script:Results.Category) Categoria creada"
    Write-Host "$(Get-StatusIcon $script:Results.Unit) Unidad obtenida"
    Write-Host "$(Get-StatusIcon $script:Results.Product) Producto creado: MART-20OZ"
    Write-Host "$(Get-StatusIcon $script:Results.StockAdjust) Stock ajustado: 50"
    Write-Host "$(Get-StatusIcon $script:Results.FinalCheck) Verificacion final"
}

# ---------- Paso 1: Login ----------

function Invoke-Login {
    Write-Host "`n--- PASO 1: LOGIN ---"

    $body = @{
        email      = $Email
        password   = $Password
        tenantSlug = $TenantSlug
    } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" `
            -Method Post `
            -ContentType "application/json" `
            -Headers @{ "X-Tenant-Slug" = $TenantSlug } `
            -Body $body `
            -ErrorAction Stop

        if (-not $response.success) {
            throw "Login respondio success=false: $($response.message)"
        }

        $script:Token = $response.data.accessToken
        Write-Host "$([char]0x2705) Login exitoso - Token obtenido" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Login"
        return $false
    }
}

# ---------- Paso 2: Crear categoria ----------

function New-CategoryViaSql {
    try {
        $schema = Get-TenantSchemaName
        $sql = "INSERT INTO ""$schema"".categories (id, name, description, is_active) " +
               "VALUES (gen_random_uuid(), 'Herramientas', 'Herramientas manuales', true) RETURNING id;"
        $result = Invoke-Psql -Sql $sql

        if ([string]::IsNullOrWhiteSpace($result)) {
            throw "El INSERT no devolvio un id."
        }

        $script:CategoryId = $result
        Write-Host "$([char]0x2705) Categoria creada (via SQL directo): $script:CategoryId" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "$([char]0x274C) ERROR creando categoria via SQL: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function New-Category {
    Write-Host "`n--- PASO 2: CREAR CATEGORIA ---"

    $body = @{ name = "Herramientas"; description = "Herramientas manuales" } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/categories" `
            -Method Post `
            -ContentType "application/json" `
            -Headers (Get-AuthHeaders) `
            -Body $body `
            -ErrorAction Stop

        $script:CategoryId = $response.data.id
        Write-Host "$([char]0x2705) Categoria creada: $script:CategoryId" -ForegroundColor Green
        return $true
    }
    catch {
        if ((Get-StatusCode $_) -eq 404) {
            Write-Host "$([char]0x26A0) POST /api/inventory/categories no existe todavia (404). Usando fallback SQL..." -ForegroundColor Yellow
            return New-CategoryViaSql
        }

        Write-ErrorDetails -ErrorRecord $_ -Step "Crear categoria"
        return $false
    }
}

# ---------- Paso 3: Obtener unit id ----------

function Get-UnitIdViaSql {
    try {
        $schema = Get-TenantSchemaName
        $sql = "SELECT id FROM ""$schema"".units WHERE abbreviation = 'und' LIMIT 1;"
        $result = Invoke-Psql -Sql $sql

        if ([string]::IsNullOrWhiteSpace($result)) {
            throw "No se encontro la unidad 'und' en el schema '$schema'."
        }

        $script:UnitId = $result
        Write-Host "$([char]0x2705) Unidad obtenida (via SQL directo): $script:UnitId" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "$([char]0x274C) ERROR obteniendo unidad via SQL: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Get-UnitId {
    Write-Host "`n--- PASO 3: OBTENER UNIT ID ---"

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/units" `
            -Method Get `
            -Headers (Get-AuthHeaders) `
            -ErrorAction Stop

        $unit = $response.data | Where-Object { $_.abbreviation -eq 'und' } | Select-Object -First 1

        if (-not $unit) {
            throw "La respuesta no trae una unidad con abbreviation 'und'."
        }

        $script:UnitId = $unit.id
        Write-Host "$([char]0x2705) Unidad obtenida: $script:UnitId" -ForegroundColor Green
        return $true
    }
    catch {
        if ((Get-StatusCode $_) -eq 404) {
            Write-Host "$([char]0x26A0) GET /api/inventory/units no existe todavia (404). Usando fallback SQL..." -ForegroundColor Yellow
            return Get-UnitIdViaSql
        }

        Write-ErrorDetails -ErrorRecord $_ -Step "Obtener unidad"
        return $false
    }
}

# ---------- Paso 4: Crear producto ----------

function New-Product {
    Write-Host "`n--- PASO 4: CREAR PRODUCTO ---"

    $body = @{
        sku         = "MART-20OZ"
        name        = "Martillo 20oz"
        description = "Martillo de carpintero profesional"
        price       = 45000
        cost        = 28000
        minStock    = 5
        categoryId  = $script:CategoryId
        unitId      = $script:UnitId
        attributes  = @{
            material = "acero"
            peso_gr  = "560"
        }
    } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/products" `
            -Method Post `
            -ContentType "application/json" `
            -Headers (Get-AuthHeaders) `
            -Body $body `
            -ErrorAction Stop

        $script:ProductId = $response.data
        Write-Host "$([char]0x2705) Producto creado: $script:ProductId" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Crear producto"
        return $false
    }
}

# ---------- Paso 5 / 7: Listar productos ----------

function Get-ProductsList {
    param([string]$StepLabel = "LISTAR PRODUCTOS")

    Write-Host "`n--- $StepLabel ---"

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/products?page=1&pageSize=50" `
            -Method Get `
            -Headers (Get-AuthHeaders) `
            -ErrorAction Stop

        $response.data.items | Format-Table sku, name, price, stock, minStock -AutoSize | Out-String | Write-Host

        Write-Host "$([char]0x2705) Total productos: $($response.data.totalCount)" -ForegroundColor Green
        return $response.data
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step $StepLabel
        return $null
    }
}

# ---------- Paso 6: Ajustar stock ----------

function Set-StockAdjustment {
    Write-Host "`n--- PASO 6: AJUSTAR STOCK ---"

    $body = @{
        quantity = 50
        type     = "InitialStock"
        reason   = "Stock inicial de apertura"
    } | ConvertTo-Json

    try {
        Invoke-RestMethod -Uri "$BaseUrl/api/inventory/products/$script:ProductId/adjust-stock" `
            -Method Post `
            -ContentType "application/json" `
            -Headers (Get-AuthHeaders) `
            -Body $body `
            -ErrorAction Stop | Out-Null

        Write-Host "$([char]0x2705) Stock ajustado correctamente" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Ajustar stock"
        return $false
    }
}

# ---------- Flujo principal ----------

Write-Host "=== SCRIPT DE PRUEBA - AXON API ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl"
Write-Host "Tenant:   $TenantSlug`n"

$script:Results.Login = Invoke-Login
if (-not $script:Results.Login) { Show-Summary; exit 1 }

$script:Results.Category = New-Category
if (-not $script:Results.Category) { Show-Summary; exit 1 }

$script:Results.Unit = Get-UnitId
if (-not $script:Results.Unit) { Show-Summary; exit 1 }

$script:Results.Product = New-Product
if (-not $script:Results.Product) { Show-Summary; exit 1 }

Get-ProductsList -StepLabel "PASO 5: LISTAR PRODUCTOS" | Out-Null

$script:Results.StockAdjust = Set-StockAdjustment
if (-not $script:Results.StockAdjust) { Show-Summary; exit 1 }

$finalList = Get-ProductsList -StepLabel "PASO 7: LISTAR PRODUCTOS (DESPUES DEL AJUSTE)"

if ($finalList) {
    $updated = $finalList.items | Where-Object { $_.id -eq $script:ProductId } | Select-Object -First 1

    if ($updated) {
        Write-Host "$([char]0x2705) Stock actual: $($updated.stock)" -ForegroundColor Green
        $script:Results.FinalCheck = $true
    }
    else {
        Write-Host "$([char]0x274C) El producto creado no aparece en la lista final." -ForegroundColor Red
        $script:Results.FinalCheck = $false
    }
}
else {
    $script:Results.FinalCheck = $false
}

Show-Summary
