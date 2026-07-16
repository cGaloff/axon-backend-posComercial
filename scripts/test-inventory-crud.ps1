<#
    Flujo de humo del CRUD de inventario (categorias, unidades, bodegas,
    definiciones de atributo y el CRUD completo de productos): login ->
    unidades -> categorias -> bodegas -> definicion de atributo -> crear
    producto -> obtener -> actualizar -> verificar -> desactivar (soft
    delete) -> verificar bloqueo.

    Requiere Windows PowerShell 5.1. No usa docker exec: todos los datos
    se obtienen directamente de la API.
#>

param(
    [string]$BaseUrl = "http://localhost:5026",
    [string]$TenantSlug = "ferreteria-don-pedro",
    [string]$Email = "admin@ferreteriadonpedro.com",
    [string]$Password = "Admin1234"
)

$ErrorActionPreference = "Stop"

$script:Token = $null
$script:UnitId = $null
$script:CategoryId = $null
$script:CategoriesInitialCount = 0
$script:AttrDefId = $null
$script:ProductId = $null

$script:Results = [ordered]@{
    Login              = $false
    GetUnits           = $false
    GetCategoriesInit  = $false
    CreateCategory     = $false
    GetCategoriesAfter = $false
    GetWarehouses      = $false
    CreateAttrDef      = $false
    GetAttrDefs        = $false
    CreateProduct      = $false
    GetProduct         = $false
    UpdateProduct      = $false
    VerifyUpdate       = $false
    DeactivateProduct  = $false
    VerifyDeactivated  = $false
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

function Get-StatusIcon {
    param([bool]$Success)
    if ($Success) { return [char]0x2705 } else { return [char]0x274C }
}

function Show-Summary {
    Write-Host "`n=== RESUMEN CRUD INVENTARIO ===" -ForegroundColor Cyan
    Write-Host "$(Get-StatusIcon $script:Results.Login) Login"
    Write-Host "$(Get-StatusIcon $script:Results.GetUnits) Unidades obtenidas"
    Write-Host "$(Get-StatusIcon $script:Results.GetCategoriesInit) Categorias iniciales obtenidas"
    Write-Host "$(Get-StatusIcon $script:Results.CreateCategory) Categoria creada"
    Write-Host "$(Get-StatusIcon $script:Results.GetCategoriesAfter) Categorias despues de crear"
    Write-Host "$(Get-StatusIcon $script:Results.GetWarehouses) Bodegas obtenidas"
    Write-Host "$(Get-StatusIcon $script:Results.CreateAttrDef) Definicion de atributo creada"
    Write-Host "$(Get-StatusIcon $script:Results.GetAttrDefs) Definiciones de atributo obtenidas"
    Write-Host "$(Get-StatusIcon $script:Results.CreateProduct) Producto creado"
    Write-Host "$(Get-StatusIcon $script:Results.GetProduct) Producto obtenido"
    Write-Host "$(Get-StatusIcon $script:Results.UpdateProduct) Producto actualizado"
    Write-Host "$(Get-StatusIcon $script:Results.VerifyUpdate) Actualizacion verificada"
    Write-Host "$(Get-StatusIcon $script:Results.DeactivateProduct) Producto desactivado"
    Write-Host "$(Get-StatusIcon $script:Results.VerifyDeactivated) Soft delete verificado"
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

# ---------- Paso 2: Obtener unidades ----------

function Get-Units {
    Write-Host "`n--- PASO 2: OBTENER UNIDADES ---"

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/units" `
            -Method Get `
            -Headers (Get-AuthHeaders) `
            -ErrorAction Stop

        $response.data | Format-Table id, name, abbreviation -AutoSize | Out-String | Write-Host

        $count = $response.data.Count
        Write-Host "$([char]0x2705) Unidades obtenidas: $count" -ForegroundColor Green

        $unit = $response.data | Where-Object { $_.abbreviation -eq 'und' } | Select-Object -First 1

        if (-not $unit) {
            throw "No se encontro ninguna unidad con abbreviation 'und'."
        }

        $script:UnitId = $unit.id
        Write-Host "$([char]0x2705) Unit 'und' id: $script:UnitId" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Obtener unidades"
        return $false
    }
}

# ---------- Paso 3: Categorias iniciales ----------

function Get-CategoriesInitial {
    Write-Host "`n--- PASO 3: OBTENER CATEGORIAS (INICIAL) ---"

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/categories" `
            -Method Get `
            -Headers (Get-AuthHeaders) `
            -ErrorAction Stop

        $script:CategoriesInitialCount = $response.data.Count
        Write-Host "$([char]0x2705) Categorias iniciales: $script:CategoriesInitialCount" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Obtener categorias iniciales"
        return $false
    }
}

# ---------- Paso 4: Crear categoria ----------

function New-Category {
    Write-Host "`n--- PASO 4: CREAR CATEGORIA ---"

    $body = @{
        name        = "Herramientas"
        description = "Herramientas manuales"
    } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/categories" `
            -Method Post `
            -ContentType "application/json" `
            -Headers (Get-AuthHeaders) `
            -Body $body `
            -ErrorAction Stop

        $script:CategoryId = $response.data
        Write-Host "$([char]0x2705) Categoria creada: $script:CategoryId" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Crear categoria"
        return $false
    }
}

# ---------- Paso 5: Categorias despues de crear ----------

function Get-CategoriesAfter {
    Write-Host "`n--- PASO 5: OBTENER CATEGORIAS (DESPUES DE CREAR) ---"

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/categories" `
            -Method Get `
            -Headers (Get-AuthHeaders) `
            -ErrorAction Stop

        $count = $response.data.Count
        Write-Host "$([char]0x2705) Categorias despues de crear: $count" -ForegroundColor Green

        $expected = $script:CategoriesInitialCount + 1
        if ($count -eq $expected) {
            Write-Host "$([char]0x2705) El conteo aumento en 1 respecto al inicial ($script:CategoriesInitialCount -> $count)" -ForegroundColor Green
            return $true
        }

        Write-Host "$([char]0x274C) Se esperaba $expected categorias, se obtuvieron $count" -ForegroundColor Red
        return $false
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Obtener categorias despues de crear"
        return $false
    }
}

# ---------- Paso 6: Obtener bodegas ----------

function Get-Warehouses {
    Write-Host "`n--- PASO 6: OBTENER BODEGAS ---"

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/warehouses" `
            -Method Get `
            -Headers (Get-AuthHeaders) `
            -ErrorAction Stop

        $response.data | Format-Table id, name, isDefault -AutoSize | Out-String | Write-Host

        $count = $response.data.Count
        Write-Host "$([char]0x2705) Bodegas obtenidas: $count" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Obtener bodegas"
        return $false
    }
}

# ---------- Paso 7: Crear definicion de atributo ----------

function New-AttributeDefinition {
    Write-Host "`n--- PASO 7: CREAR DEFINICION DE ATRIBUTO ---"

    $body = @{
        key          = "material"
        label        = "Material"
        type         = "text"
        options      = $null
        categoryId   = $script:CategoryId
        isFilterable = $true
        sortOrder    = 1
    } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/attribute-definitions" `
            -Method Post `
            -ContentType "application/json" `
            -Headers (Get-AuthHeaders) `
            -Body $body `
            -ErrorAction Stop

        $script:AttrDefId = $response.data
        Write-Host "$([char]0x2705) Atributo definido: $script:AttrDefId" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Crear definicion de atributo"
        return $false
    }
}

# ---------- Paso 8: Obtener definiciones de atributo ----------

function Get-AttributeDefinitions {
    Write-Host "`n--- PASO 8: OBTENER DEFINICIONES DE ATRIBUTO ---"

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/attribute-definitions?categoryId=$script:CategoryId" `
            -Method Get `
            -Headers (Get-AuthHeaders) `
            -ErrorAction Stop

        $count = $response.data.Count
        $found = $response.data | Where-Object { $_.id -eq $script:AttrDefId } | Select-Object -First 1

        if (-not $found) {
            Write-Host "$([char]0x274C) El atributo creado ($script:AttrDefId) no aparece en la lista." -ForegroundColor Red
            return $false
        }

        Write-Host "$([char]0x2705) Atributos para la categoria: $count" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Obtener definiciones de atributo"
        return $false
    }
}

# ---------- Paso 9: Crear producto (con atributo) ----------

function New-Product {
    Write-Host "`n--- PASO 9: CREAR PRODUCTO (CON ATRIBUTO) ---"

    $body = @{
        sku         = "MART-TEST-001"
        name        = "Martillo Test CRUD"
        description = "Producto para probar CRUD"
        price       = 45000
        cost        = 28000
        minStock    = 3
        categoryId  = $script:CategoryId
        unitId      = $script:UnitId
        attributes  = @{ material = "acero" }
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

# ---------- Paso 10: Obtener producto por id ----------

function Get-ProductById {
    Write-Host "`n--- PASO 10: OBTENER PRODUCTO POR ID ---"

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/products/$script:ProductId" `
            -Method Get `
            -Headers (Get-AuthHeaders) `
            -ErrorAction Stop

        $data = $response.data

        if ($data.id -ne $script:ProductId) {
            Write-Host "$([char]0x274C) El producto retornado no coincide con el id esperado." -ForegroundColor Red
            return $false
        }

        Write-Host "$([char]0x2705) Producto obtenido: $($data.name) - `$$($data.price)" -ForegroundColor Green
        Write-Host "$([char]0x2705) Atributo material: $($data.attributes.material)" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Obtener producto por id"
        return $false
    }
}

# ---------- Paso 11: Actualizar producto ----------

function Update-Product {
    Write-Host "`n--- PASO 11: ACTUALIZAR PRODUCTO ---"

    $body = @{
        name        = "Martillo Test CRUD Actualizado"
        description = "Descripcion actualizada"
        price       = 50000
        cost        = 30000
        minStock    = 5
        categoryId  = $script:CategoryId
        unitId      = $script:UnitId
        attributes  = @{ material = "titanio" }
    } | ConvertTo-Json

    try {
        Invoke-RestMethod -Uri "$BaseUrl/api/inventory/products/$script:ProductId" `
            -Method Put `
            -ContentType "application/json" `
            -Headers (Get-AuthHeaders) `
            -Body $body `
            -ErrorAction Stop | Out-Null

        Write-Host "$([char]0x2705) Producto actualizado" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Actualizar producto"
        return $false
    }
}

# ---------- Paso 12: Verificar actualizacion ----------

function Test-ProductUpdated {
    Write-Host "`n--- PASO 12: VERIFICAR ACTUALIZACION ---"

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/products/$script:ProductId" `
            -Method Get `
            -Headers (Get-AuthHeaders) `
            -ErrorAction Stop

        $data = $response.data

        $nameOk = $data.name -eq "Martillo Test CRUD Actualizado"
        $priceOk = $data.price -eq 50000
        $attributeOk = $data.attributes.material -eq "titanio"

        if ($nameOk -and $priceOk -and $attributeOk) {
            Write-Host "$([char]0x2705) Actualizacion verificada: $($data.name) - `$$($data.price)" -ForegroundColor Green
            return $true
        }

        Write-Host "$([char]0x274C) La actualizacion no coincide con lo esperado:" -ForegroundColor Red
        Write-Host "   name=$($data.name) (esperado 'Martillo Test CRUD Actualizado')" -ForegroundColor Red
        Write-Host "   price=$($data.price) (esperado 50000)" -ForegroundColor Red
        Write-Host "   attributes.material=$($data.attributes.material) (esperado 'titanio')" -ForegroundColor Red
        return $false
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Verificar actualizacion"
        return $false
    }
}

# ---------- Paso 13: Desactivar producto (soft delete) ----------

function Remove-Product {
    Write-Host "`n--- PASO 13: DESACTIVAR PRODUCTO (SOFT DELETE) ---"

    try {
        Invoke-RestMethod -Uri "$BaseUrl/api/inventory/products/$script:ProductId" `
            -Method Delete `
            -Headers (Get-AuthHeaders) `
            -ErrorAction Stop | Out-Null

        Write-Host "$([char]0x2705) Producto desactivado" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Desactivar producto"
        return $false
    }
}

# ---------- Paso 14: Verificar desactivacion ----------

function Test-ProductDeactivated {
    Write-Host "`n--- PASO 14: VERIFICAR PRODUCTO DESACTIVADO ---"

    try {
        Invoke-RestMethod -Uri "$BaseUrl/api/inventory/products/$script:ProductId" `
            -Method Get `
            -Headers (Get-AuthHeaders) `
            -ErrorAction Stop | Out-Null

        Write-Host "$([char]0x274C) ERROR: producto sigue activo (se esperaba 400)." -ForegroundColor Red
        return $false
    }
    catch {
        $statusCode = Get-StatusCode $_

        if ($statusCode -eq 400) {
            Write-Host "$([char]0x2705) Soft delete verificado" -ForegroundColor Green
            return $true
        }

        Write-ErrorDetails -ErrorRecord $_ -Step "Verificar producto desactivado"
        return $false
    }
}

# ---------- Flujo principal ----------

Write-Host "=== TEST CRUD INVENTARIO - AXON API ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl"
Write-Host "Tenant:   $TenantSlug`n"

$script:Results.Login = Invoke-Login
if (-not $script:Results.Login) {
    Show-Summary
    Read-Host "`nPresiona Enter para cerrar"
    exit 1
}

$script:Results.GetUnits = Get-Units
if (-not $script:Results.GetUnits) {
    Show-Summary
    Read-Host "`nPresiona Enter para cerrar"
    exit 1
}

$script:Results.GetCategoriesInit = Get-CategoriesInitial

$script:Results.CreateCategory = New-Category
if (-not $script:Results.CreateCategory) {
    Show-Summary
    Read-Host "`nPresiona Enter para cerrar"
    exit 1
}

$script:Results.GetCategoriesAfter = Get-CategoriesAfter
$script:Results.GetWarehouses = Get-Warehouses
$script:Results.CreateAttrDef = New-AttributeDefinition
$script:Results.GetAttrDefs = Get-AttributeDefinitions

$script:Results.CreateProduct = New-Product
if (-not $script:Results.CreateProduct) {
    Show-Summary
    Read-Host "`nPresiona Enter para cerrar"
    exit 1
}

$script:Results.GetProduct = Get-ProductById
$script:Results.UpdateProduct = Update-Product
$script:Results.VerifyUpdate = Test-ProductUpdated
$script:Results.DeactivateProduct = Remove-Product
$script:Results.VerifyDeactivated = Test-ProductDeactivated

Show-Summary

Read-Host "`nPresiona Enter para cerrar"
