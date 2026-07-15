<#
    Flujo de humo del modulo de caja: login -> caja por defecto -> abrir sesion ->
    setup de catalogo (SQL) -> crear producto -> stock inicial -> venta efectivo ->
    venta tarjeta -> egreso manual -> resumen de sesion -> cerrar sesion ->
    verificar que una venta sin sesion activa queda bloqueada.

    Requiere Windows PowerShell 5.1 y Docker corriendo (fallback SQL para
    unidad/categoria, ya que esos endpoints todavia no existen).
#>

param(
    [string]$BaseUrl = "http://localhost:5026",
    [string]$TenantSlug = "ferreteria-don-pedro",
    [string]$Email = "admin@ferreteriadonpedro.com",
    [string]$Password = "Admin1234",
    [string]$DefaultUserId = "00000000-0000-0000-0000-000000000001",
    [string]$PostgresContainer = "axon_postgres",
    [string]$PostgresUser = "axon_user",
    # OJO: appsettings.Development.json usa "axon_master"; si tu docker-compose.yml
    # sigue creando "axon_db", pasa -DatabaseName axon_db.
    [string]$DatabaseName = "axon_master"
)

$ErrorActionPreference = "Stop"

$script:Token = $null
$script:SchemaName = $null
$script:CashRegisterId = $null
$script:SessionId = $null
$script:UnitId = $null
$script:CategoryId = $null
$script:ProductId = $null
$script:SaleId1 = $null
$script:SaleId2 = $null

$script:MoneyFormat = New-Object System.Globalization.NumberFormatInfo
$script:MoneyFormat.NumberGroupSeparator = "."
$script:MoneyFormat.NumberDecimalDigits = 0

$script:Results = [ordered]@{
    Login          = $false
    CashRegister   = $false
    OpenSession    = $false
    Setup          = $false
    Product        = $false
    InitialStock   = $false
    CashSale       = $false
    CardSale       = $false
    ManualExpense  = $false
    SessionSummary = $false
    CloseSession   = $false
    SessionControl = $false
}

# ---------- Helpers ----------

function Format-Money {
    param([Parameter(Mandatory)] [decimal]$Amount)

    $sign = if ($Amount -lt 0) { "-" } else { "" }
    $abs = [Math]::Abs($Amount)
    return "$sign`$" + $abs.ToString("N0", $script:MoneyFormat)
}

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

    $script:SchemaName = $result.Trim().Replace('{', '').Replace('}', '')
    Write-Host "   (schema del tenant: $script:SchemaName)" -ForegroundColor DarkGray
    return $script:SchemaName
}

function Show-Summary {
    Write-Host "`n=== RESUMEN MODULO CAJA ===" -ForegroundColor Cyan
    Write-Host "$(Get-StatusIcon $script:Results.Login) Login"
    Write-Host "$(Get-StatusIcon $script:Results.CashRegister) Caja por defecto obtenida"
    Write-Host "$(Get-StatusIcon $script:Results.OpenSession) Sesion de caja abierta"
    Write-Host "$(Get-StatusIcon $script:Results.Setup) Setup (schema / unidad / categoria)"
    Write-Host "$(Get-StatusIcon $script:Results.Product) Producto creado"
    Write-Host "$(Get-StatusIcon $script:Results.InitialStock) Stock inicial: 10"
    Write-Host "$(Get-StatusIcon $script:Results.CashSale) Venta en efectivo"
    Write-Host "$(Get-StatusIcon $script:Results.CardSale) Venta con tarjeta"
    Write-Host "$(Get-StatusIcon $script:Results.ManualExpense) Egreso manual"
    Write-Host "$(Get-StatusIcon $script:Results.SessionSummary) Resumen de sesion (ExpectedAmount = 75000)"
    Write-Host "$(Get-StatusIcon $script:Results.CloseSession) Sesion cerrada"
    Write-Host "$(Get-StatusIcon $script:Results.SessionControl) Control de sesion (venta bloqueada sin sesion activa)"
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

# ---------- Paso 2: Obtener caja por defecto ----------

function Get-DefaultCashRegister {
    Write-Host "`n--- PASO 2: OBTENER CAJA POR DEFECTO ---"

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/cash-register" `
            -Method Get `
            -Headers (Get-AuthHeaders) `
            -ErrorAction Stop

        $register = $response.data | Where-Object { $_.isDefault -eq $true } | Select-Object -First 1

        if (-not $register) {
            throw "No se encontro ninguna caja con isDefault = true."
        }

        $script:CashRegisterId = $register.id
        Write-Host "$([char]0x2705) Caja encontrada: $($register.name) - $script:CashRegisterId" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Obtener caja por defecto"
        return $false
    }
}

# ---------- Paso 3: Abrir sesion de caja ----------

function Open-CashSession {
    Write-Host "`n--- PASO 3: ABRIR SESION DE CAJA ---"

    $body = @{
        cashRegisterId = $script:CashRegisterId
        openedBy       = $DefaultUserId
        initialAmount  = 50000
    } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/cash-register/sessions/open" `
            -Method Post `
            -ContentType "application/json" `
            -Headers (Get-AuthHeaders) `
            -Body $body `
            -ErrorAction Stop

        $script:SessionId = $response.data.sessionId
        Write-Host "$([char]0x2705) Sesion abierta: $script:SessionId" -ForegroundColor Green
        Write-Host "$([char]0x2705) Monto inicial: $(Format-Money $response.data.initialAmount)" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Abrir sesion de caja"
        return $false
    }
}

# ---------- Paso 4: Setup (SQL directo) ----------

function Get-UnitId {
    try {
        $schema = Get-TenantSchemaName
        $sql = "SELECT id FROM ""$schema"".units WHERE abbreviation = 'und' LIMIT 1;"
        $result = Invoke-Psql -Sql $sql

        if ([string]::IsNullOrWhiteSpace($result)) {
            throw "No se encontro la unidad 'und'."
        }

        $script:UnitId = $result
        $script:UnitId = $script:UnitId.Trim().Replace('{', '').Replace('}', '').Replace("`n", '').Replace("`r", '')
        Write-Host "$([char]0x2705) Unit id (und): $script:UnitId" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "$([char]0x274C) ERROR obteniendo unit id: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Get-OrCreateCategoryId {
    # "categories.name" no tiene UNIQUE en el schema del tenant: se busca primero
    # y solo se inserta si no existe, para que el script sea idempotente.
    try {
        $schema = Get-TenantSchemaName

        $selectSql = "SELECT id FROM ""$schema"".categories WHERE name = 'Herramientas' LIMIT 1;"
        $existing = Invoke-Psql -Sql $selectSql

        if (-not [string]::IsNullOrWhiteSpace($existing)) {
            $script:CategoryId = $existing
            $script:CategoryId = $script:CategoryId.Trim().Replace('{', '').Replace('}', '').Replace("`n", '').Replace("`r", '')
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
        $script:CategoryId = $script:CategoryId.Trim().Replace('{', '').Replace('}', '').Replace("`n", '').Replace("`r", '')
        Write-Host "$([char]0x2705) Categoria creada: $script:CategoryId" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "$([char]0x274C) ERROR obteniendo/creando categoria: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Invoke-Setup {
    Write-Host "`n--- PASO 4: SETUP (SQL directo via docker exec) ---"

    try {
        Get-TenantSchemaName | Out-Null
    }
    catch {
        Write-Host "$([char]0x274C) ERROR obteniendo el schema del tenant: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }

    $unitOk = Get-UnitId
    $categoryOk = Get-OrCreateCategoryId

    return ($unitOk -and $categoryOk)
}

# ---------- Paso 5: Crear producto ----------

function New-Product {
    Write-Host "`n--- PASO 5: CREAR PRODUCTO DE PRUEBA ---"

    $body = @{
        sku         = "MARTI-001"
        name        = "Martillo Test"
        description = "Producto para prueba de caja"
        price       = 30000
        cost        = 18000
        minStock    = 2
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

# ---------- Paso 6: Ajustar stock inicial ----------

function Set-InitialStock {
    Write-Host "`n--- PASO 6: AJUSTAR STOCK INICIAL ---"

    $body = @{
        quantity = 10
        type     = "InitialStock"
        reason   = "Stock inicial test caja"
    } | ConvertTo-Json

    try {
        Invoke-RestMethod -Uri "$BaseUrl/api/inventory/products/$script:ProductId/adjust-stock" `
            -Method Post `
            -ContentType "application/json" `
            -Headers (Get-AuthHeaders) `
            -Body $body `
            -ErrorAction Stop | Out-Null

        Write-Host "$([char]0x2705) Stock inicial: 10 unidades" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Ajustar stock inicial"
        return $false
    }
}

# ---------- Paso 7: Venta en efectivo ----------

function New-CashSale {
    Write-Host "`n--- PASO 7: PROCESAR VENTA EN EFECTIVO ---"

    $body = @{
        items = @(
            @{ productId = $script:ProductId; quantity = 1; discount = 0 }
        )
        paymentMethod  = "Cash"
        cashRegisterId = $script:CashRegisterId
        createdBy      = $DefaultUserId
        amountPaid     = 50000
        customerId     = $null
        customerName   = "Cliente Test"
        customerEmail  = $null
        notes          = "Venta test caja"
    } | ConvertTo-Json -Depth 5

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/sales" `
            -Method Post `
            -ContentType "application/json" `
            -Headers (Get-AuthHeaders) `
            -Body $body `
            -ErrorAction Stop

        $script:SaleId1 = $response.data.saleId
        Write-Host "$([char]0x2705) Venta efectivo: $($response.data.saleNumber) - $(Format-Money $response.data.total)" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Procesar venta en efectivo"
        return $false
    }
}

# ---------- Paso 8: Venta con tarjeta ----------

function New-CardSale {
    Write-Host "`n--- PASO 8: PROCESAR VENTA CON TARJETA ---"

    $body = @{
        items = @(
            @{ productId = $script:ProductId; quantity = 1; discount = 0 }
        )
        paymentMethod  = "Card"
        cashRegisterId = $script:CashRegisterId
        createdBy      = $DefaultUserId
        amountPaid     = 0
        customerId     = $null
        customerName   = "Cliente Test"
        customerEmail  = $null
        notes          = "Venta test caja"
    } | ConvertTo-Json -Depth 5

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/sales" `
            -Method Post `
            -ContentType "application/json" `
            -Headers (Get-AuthHeaders) `
            -Body $body `
            -ErrorAction Stop

        $script:SaleId2 = $response.data.saleId
        Write-Host "$([char]0x2705) Venta tarjeta: $($response.data.saleNumber) - $(Format-Money $response.data.total)" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Procesar venta con tarjeta"
        return $false
    }
}

# ---------- Paso 9: Egreso manual ----------

function Add-ManualExpense {
    Write-Host "`n--- PASO 9: AGREGAR EGRESO MANUAL ---"

    $body = @{
        type        = "Expense"
        amount      = 5000
        description = "Pago domicilio"
        createdBy   = $DefaultUserId
    } | ConvertTo-Json

    try {
        Invoke-RestMethod -Uri "$BaseUrl/api/cash-register/sessions/$script:SessionId/movements" `
            -Method Post `
            -ContentType "application/json" `
            -Headers (Get-AuthHeaders) `
            -Body $body `
            -ErrorAction Stop | Out-Null

        Write-Host "$([char]0x2705) Egreso registrado: -$(Format-Money 5000)" -ForegroundColor Green
        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Agregar egreso manual"
        return $false
    }
}

# ---------- Paso 10: Resumen de sesion ----------

function Get-SessionSummary {
    Write-Host "`n--- PASO 10: CONSULTAR RESUMEN DE SESION ---"

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/cash-register/sessions/$script:SessionId/summary" `
            -Method Get `
            -Headers (Get-AuthHeaders) `
            -ErrorAction Stop

        $data = $response.data

        Write-Host "`n=== RESUMEN DE SESION ===" -ForegroundColor Cyan
        Write-Host "Monto inicial:        $(Format-Money $data.initialAmount)"
        Write-Host "Ventas efectivo:      $(Format-Money $data.totalCashSales)"
        Write-Host "Ventas tarjeta:       $(Format-Money $data.totalCardSales) (informativo)"
        Write-Host "Egresos:              -$(Format-Money $data.totalExpenses)"
        Write-Host "Monto esperado:       $(Format-Money $data.expectedAmount)"

        $expected = 75000
        if ($data.expectedAmount -eq $expected) {
            Write-Host "$([char]0x2705) ExpectedAmount coincide: $(Format-Money $data.expectedAmount) (esperado: $(Format-Money $expected))" -ForegroundColor Green
            return $true
        }

        Write-Host "$([char]0x274C) ExpectedAmount NO coincide: $(Format-Money $data.expectedAmount) (esperado: $(Format-Money $expected))" -ForegroundColor Red
        return $false
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Consultar resumen de sesion"
        return $false
    }
}

# ---------- Paso 11: Cerrar sesion de caja ----------

function Close-CashSession {
    Write-Host "`n--- PASO 11: CERRAR SESION DE CAJA ---"

    $body = @{
        closedBy      = $DefaultUserId
        countedAmount = 72000
        notes         = "Cierre de prueba"
        forceClose    = $false
    } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/cash-register/sessions/$script:SessionId/close" `
            -Method Post `
            -ContentType "application/json" `
            -Headers (Get-AuthHeaders) `
            -Body $body `
            -ErrorAction Stop

        $data = $response.data

        $differenceLabel = if ($data.difference -lt 0) { "(falta dinero)" } elseif ($data.difference -gt 0) { "(sobra dinero)" } else { "(cuadra)" }

        Write-Host "$([char]0x2705) Sesion cerrada" -ForegroundColor Green
        Write-Host "$([char]0x2705) Monto esperado:  $(Format-Money $data.expectedAmount)" -ForegroundColor Green
        Write-Host "$([char]0x2705) Monto contado:   $(Format-Money $data.countedAmount)" -ForegroundColor Green
        Write-Host "$([char]0x2705) Descuadre:       $(Format-Money $data.difference) $differenceLabel" -ForegroundColor Green

        return $true
    }
    catch {
        Write-ErrorDetails -ErrorRecord $_ -Step "Cerrar sesion de caja"
        return $false
    }
}

# ---------- Paso 12: Verificar bloqueo sin sesion activa ----------

function Test-SaleBlockedWithoutSession {
    Write-Host "`n--- PASO 12: VERIFICAR QUE NO SE PUEDE VENDER SIN SESION ACTIVA ---"

    $body = @{
        items = @(
            @{ productId = $script:ProductId; quantity = 1; discount = 0 }
        )
        paymentMethod  = "Cash"
        cashRegisterId = $script:CashRegisterId
        createdBy      = $DefaultUserId
        amountPaid     = 50000
        customerId     = $null
        customerName   = "Cliente Test"
        customerEmail  = $null
        notes          = "Venta test caja"
    } | ConvertTo-Json -Depth 5

    try {
        Invoke-RestMethod -Uri "$BaseUrl/api/sales" `
            -Method Post `
            -ContentType "application/json" `
            -Headers (Get-AuthHeaders) `
            -Body $body `
            -ErrorAction Stop | Out-Null

        Write-Host "$([char]0x274C) ERROR: se permitio procesar una venta sin sesion de caja activa (se esperaba 400)." -ForegroundColor Red
        return $false
    }
    catch {
        $statusCode = Get-StatusCode $_

        if ($statusCode -eq 400) {
            Write-Host "$([char]0x2705) Control de sesion funciona (400 Bad Request)" -ForegroundColor Green
            return $true
        }

        Write-ErrorDetails -ErrorRecord $_ -Step "Verificar bloqueo de venta sin sesion activa"
        return $false
    }
}

# ---------- Flujo principal ----------

Write-Host "=== TEST MODULO DE CAJA - AXON API ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl"
Write-Host "Tenant:   $TenantSlug`n"

$script:Results.Login = Invoke-Login
if (-not $script:Results.Login) {
    Show-Summary
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

$script:Results.CashRegister = Get-DefaultCashRegister
if (-not $script:Results.CashRegister) {
    Show-Summary
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

$script:Results.OpenSession = Open-CashSession
if (-not $script:Results.OpenSession) {
    Show-Summary
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

$script:Results.Setup = Invoke-Setup

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

$script:Results.CashSale = New-CashSale
$script:Results.CardSale = New-CardSale
$script:Results.ManualExpense = Add-ManualExpense
$script:Results.SessionSummary = Get-SessionSummary
$script:Results.CloseSession = Close-CashSession
$script:Results.SessionControl = Test-SaleBlockedWithoutSession

Show-Summary

Read-Host "`nPresiona Enter para cerrar"
