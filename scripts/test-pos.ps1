<#
    Flujo de humo completo de POS: login -> setup (SQL) -> crear producto ->
    stock inicial -> venta en efectivo -> verificar stock -> historial ->
    recibo PDF -> devolucion -> verificar stock restaurado.

    Requiere Windows PowerShell 5.1 y Docker corriendo (para el fallback SQL
    de categoria/unidad/bodega, ya que esos endpoints no existen todavia).
#>

param(
    [string]$BaseUrl = "http://localhost:5026",
    [string]$TenantSlug = "ferreteria-don-pedro",
    [string]$Email = "admin@ferreteriadonpedro.com",
    [string]$Password = "Admin1234",
    [string]$PostgresContainer = "axon_postgres",
    [string]$PostgresUser = "axon_user",
    # OJO: appsettings.Development.json usa "axon_master" pero docker-compose.yml
    # crea "axon_db" -- si ese mismatch sigue sin resolverse, pasa -DatabaseName axon_db.
    [string]$DatabaseName = "axon_master"
)

$ErrorActionPreference = "Stop"

$script:Token = $null
$script:SchemaName = $null
$script:UnitId = $null
$script:CategoryId = $null
$script:WarehouseId = $null
$script:ProductId = $null
$script:SaleId = $null
$script:SaleNumber = $null

$script:Results = [ordered]@{
    Login            = $false
    Setup            = $false
    Product          = $false
    InitialStock     = $false
    Sale             = $false
    StockAfterSale   = $false
    History          = $false
    Receipt          = $false
    Return           = $false
    StockAfterReturn = $false
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

function Get-StatusIcon {
    param([bool]$Success)
    if ($Success) { return [char]0x2705 } else { return [char]0x274C }
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

function Show-Summary {
    Write-Host "`n=== RESUMEN POS ===" -ForegroundColor Cyan
    Write-Host "$(Get-StatusIcon $script:Results.Login) Login"
    Write-Host "$(Get-StatusIcon $script:Results.Setup) Setup (schema / unidad / categoria / bodega)"
    Write-Host "$(Get-StatusIcon $script:Results.Product) Producto creado"
    Write-Host "$(Get-StatusIcon $script:Results.InitialStock) Stock inicial: 20"
    Write-Host "$(Get-StatusIcon $script:Results.Sale) Venta procesada"
    Write-Host "$(Get-StatusIcon $script:Results.StockAfterSale) Stock post-venta: 18"
    Write-Host "$(Get-StatusIcon $script:Results.History) Historial de ventas"
    Write-Host "$(Get-StatusIcon $script:Results.Receipt) Recibo PDF"
    Write-Host "$(Get-StatusIcon $script:Results.Return) Devolucion procesada"
    Write-Host "$(Get-StatusIcon $script:Results.StockAfterReturn) Stock restaurado: 20"
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
        Write-Host "$([char]0x2705) Login exitoso" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Login"
        return $false
    }
}

# ---------- Paso 2: Setup (SQL directo) ----------

function Get-UnitId {
    try {
        $schema = Get-TenantSchemaName
        $sql = "SELECT id FROM ""$schema"".units WHERE abbreviation = 'und' LIMIT 1;"
        $result = Invoke-Psql -Sql $sql

        if ([string]::IsNullOrWhiteSpace($result)) {
            throw "No se encontro la unidad 'und'."
        }

        $script:UnitId = $result

        $script:UnitId = $script:UnitId.Trim().Replace('{','').Replace('}','')
        Write-Host "$([char]0x2705) Unit id (und): $script:UnitId" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "$([char]0x274C) ERROR obteniendo unit id: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Get-OrCreateCategoryId {
    # NOTA: "categories.name" no tiene constraint UNIQUE en el schema del tenant,
    # asi que "ON CONFLICT DO NOTHING" (como lo pediste literal) nunca dispara y
    # cada corrida insertaria una fila duplicada. En su lugar, se busca primero
    # y solo se inserta si no existe -- asi el script es realmente idempotente.
    try {
        $schema = Get-TenantSchemaName

        $selectSql = "SELECT id FROM ""$schema"".categories WHERE name = 'Herramientas' LIMIT 1;"
        $existing = Invoke-Psql -Sql $selectSql

        if (-not [string]::IsNullOrWhiteSpace($existing)) {
            $script:CategoryId = $existing
            $script:CategoryId = $script:CategoryId.Trim().Replace('{','').Replace('}','')
            Write-Host "$([char]0x2705) Categoria existente: $script:CategoryId" -ForegroundColor Green
            return $true
        }

        $insertSql = "INSERT INTO ""$schema"".categories (id, name, description, is_active) " +
                     "VALUES (gen_random_uuid(), 'Herramientas', 'Herramientas manuales', true) RETURNING id;"
        $inserted = Invoke-Psql -Sql $insertSql

        if ([string]::IsNullOrWhiteSpace($inserted)) {
            throw "El INSERT de categoria no devolvio un id."
        }

        $script:CategoryId = $inserted
        $script:CategoryId = $script:CategoryId.Trim().Replace('{','').Replace('}','')
        Write-Host "$([char]0x2705) Categoria creada: $script:CategoryId" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "$([char]0x274C) ERROR obteniendo/creando categoria: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Get-DefaultWarehouseId {
    try {
        $schema = Get-TenantSchemaName
        $sql = "SELECT id FROM ""$schema"".warehouses WHERE is_default = true LIMIT 1;"
        $result = Invoke-Psql -Sql $sql

        if ([string]::IsNullOrWhiteSpace($result)) {
            throw "No se encontro una bodega por defecto."
        }

        $script:WarehouseId = $result
        $script:WarehouseId = $script:WarehouseId.Trim().Replace('{','').Replace('}','')
        Write-Host "$([char]0x2705) Warehouse id (default): $script:WarehouseId" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "$([char]0x274C) ERROR obteniendo warehouse id: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Invoke-Setup {
    Write-Host "`n--- PASO 2: SETUP (SQL directo via docker exec) ---"

    try {
        Get-TenantSchemaName | Out-Null
    }
    catch {
        Write-Host "$([char]0x274C) ERROR obteniendo el schema del tenant: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }

    $unitOk = Get-UnitId
    $categoryOk = Get-OrCreateCategoryId
    $warehouseOk = Get-DefaultWarehouseId

    return ($unitOk -and $categoryOk -and $warehouseOk)
}

# ---------- Paso 3: Crear producto ----------

function New-Product {
    Write-Host "`n--- PASO 3: CREAR PRODUCTO DE PRUEBA ---"

    $body = @{
        sku         = "MART-001"
        name        = "Martillo Carpintero"
        description = "Martillo profesional 20oz"
        price       = 45000
        cost        = 28000
        minStock    = 3
        categoryId  = $script:CategoryId
        unitId      = $script:UnitId
    } | ConvertTo-Json -Depth 5

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

# ---------- Paso 4: Ajustar stock inicial ----------

function Set-InitialStock {
    Write-Host "`n--- PASO 4: AJUSTAR STOCK INICIAL ---"

    $body = @{
        quantity = 20
        type     = "InitialStock"
        reason   = "Stock inicial"
    } | ConvertTo-Json

    try {
        Invoke-RestMethod -Uri "$BaseUrl/api/inventory/products/$script:ProductId/adjust-stock" `
            -Method Post `
            -ContentType "application/json" `
            -Headers (Get-AuthHeaders) `
            -Body $body `
            -ErrorAction Stop | Out-Null

        Write-Host "$([char]0x2705) Stock inicial: 20 unidades" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Ajustar stock inicial"
        return $false
    }
}

# ---------- Paso 5: Procesar venta en efectivo ----------

function New-Sale {
    Write-Host "`n--- PASO 5: PROCESAR VENTA EN EFECTIVO ---"

    $body = @{
        items = @(
            @{
                productId = $script:ProductId
                quantity  = 2
                discount  = 0
            }
        )
        paymentMethod  = "Cash"
        cashRegisterId = "00000000-0000-0000-0000-000000000001"
        createdBy      = "00000000-0000-0000-0000-000000000001"
        amountPaid     = 100000
        customerId     = $null
        customerName   = "Juan Pérez"
        customerEmail  = $null
        notes          = "Venta de prueba"
    } | ConvertTo-Json -Depth 5

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/sales" `
            -Method Post `
            -ContentType "application/json" `
            -Headers (Get-AuthHeaders) `
            -Body $body `
            -ErrorAction Stop

        $script:SaleId = $response.data.saleId
        $script:SaleNumber = $response.data.saleNumber

        Write-Host "$([char]0x2705) Venta procesada: $($response.data.saleNumber) - Total: $($response.data.total)" -ForegroundColor Green
        Write-Host "$([char]0x2705) Cambio: $($response.data.change)" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Procesar venta"
        return $false
    }
}

# ---------- Paso 6: Verificar stock descontado ----------

function Test-StockAfterSale {
    Write-Host "`n--- PASO 6: VERIFICAR STOCK DESCONTADO ---"

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/products?page=1&pageSize=50" `
            -Method Get `
            -Headers (Get-AuthHeaders) `
            -ErrorAction Stop

        $product = $response.data.items | Where-Object { $_.id -eq $script:ProductId } | Select-Object -First 1

        if (-not $product) {
            throw "El producto no aparece en el listado."
        }

        $expected = 18
        if ($product.stock -eq $expected) {
            Write-Host "$([char]0x2705) Stock despues de venta: $($product.stock) (esperado: $expected)" -ForegroundColor Green
            return $true
        }

        Write-Host "$([char]0x274C) Stock despues de venta: $($product.stock) (esperado: $expected)" -ForegroundColor Red
        return $false
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Verificar stock despues de venta"
        return $false
    }
}

# ---------- Paso 7: Historial de ventas ----------

function Get-SalesHistory {
    Write-Host "`n--- PASO 7: HISTORIAL DE VENTAS ---"

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/sales?page=1&pageSize=20" `
            -Method Get `
            -Headers (Get-AuthHeaders) `
            -ErrorAction Stop

        $first = $response.data.items | Select-Object -First 1
        if ($first) {
            $first | Format-List saleNumber, customerName, paymentMethod, status, total, createdAt | Out-String | Write-Host
        }

        Write-Host "$([char]0x2705) Total ventas: $($response.data.totalCount)" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Historial de ventas"
        return $false
    }
}

# ---------- Paso 8: Recibo PDF ----------

function Get-SaleReceipt {
    Write-Host "`n--- PASO 8: OBTENER RECIBO PDF ---"

    try {
        $outFile = Join-Path (Get-Location) "test-receipt.pdf"

        Invoke-WebRequest -Uri "$BaseUrl/api/sales/$script:SaleId/receipt" `
            -Method Get `
            -Headers (Get-AuthHeaders) `
            -OutFile $outFile `
            -ErrorAction Stop | Out-Null

        Write-Host "$([char]0x2705) PDF guardado en $outFile" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Obtener recibo PDF"
        return $false
    }
}

# ---------- Paso 9: Procesar devolucion ----------

function New-SaleReturn {
    Write-Host "`n--- PASO 9: PROCESAR DEVOLUCION ---"

    $body = @{
        reason     = "Cliente no quedó satisfecho"
        returnedBy = "00000000-0000-0000-0000-000000000001"
    } | ConvertTo-Json

    try {
        Invoke-RestMethod -Uri "$BaseUrl/api/sales/$script:SaleId/return" `
            -Method Post `
            -ContentType "application/json" `
            -Headers (Get-AuthHeaders) `
            -Body $body `
            -ErrorAction Stop | Out-Null

        Write-Host "$([char]0x2705) Devolucion procesada" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Procesar devolucion"
        return $false
    }
}

# ---------- Paso 10: Verificar stock restaurado ----------

function Test-StockAfterReturn {
    Write-Host "`n--- PASO 10: VERIFICAR STOCK RESTAURADO ---"

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/products?page=1&pageSize=50" `
            -Method Get `
            -Headers (Get-AuthHeaders) `
            -ErrorAction Stop

        $product = $response.data.items | Where-Object { $_.id -eq $script:ProductId } | Select-Object -First 1

        if (-not $product) {
            throw "El producto no aparece en el listado."
        }

        $expected = 20
        if ($product.stock -eq $expected) {
            Write-Host "$([char]0x2705) Stock restaurado: $($product.stock) (esperado: $expected)" -ForegroundColor Green
            return $true
        }

        Write-Host "$([char]0x274C) Stock restaurado: $($product.stock) (esperado: $expected)" -ForegroundColor Red
        return $false
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Verificar stock restaurado"
        return $false
    }
}

# ---------- Flujo principal ----------

Write-Host "=== TEST POS - AXON API ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl"
Write-Host "Tenant:   $TenantSlug`n"

$script:Results.Login = Invoke-Login
if (-not $script:Results.Login) {
    Show-Summary
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

$script:Results.Setup = Invoke-Setup
if (-not $script:Results.Setup) {
    Show-Summary
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

$script:Results.Product = New-Product
if (-not $script:Results.Product) {
    Show-Summary
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

$script:Results.InitialStock = Set-InitialStock
if (-not $script:Results.InitialStock) {
    Show-Summary
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

$script:Results.Sale = New-Sale

if ($script:Results.Sale) {
    $script:Results.StockAfterSale = Test-StockAfterSale
}

$script:Results.History = Get-SalesHistory

if ($script:Results.Sale) {
    $script:Results.Receipt = Get-SaleReceipt
    $script:Results.Return = New-SaleReturn

    if ($script:Results.Return) {
        $script:Results.StockAfterReturn = Test-StockAfterReturn
    }
}

Show-Summary

Read-Host "`nPresiona Enter para cerrar"
