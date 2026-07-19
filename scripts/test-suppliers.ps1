<#
    Flujo de humo del modulo de Proveedores / Ordenes de Compra: login ->
    crear proveedor -> crear producto propio -> crear orden de compra ->
    recibir en dos tandas (verifica el Costo Promedio Ponderado) ->
    registrar pago -> extracto de cuenta.

    Requiere Windows PowerShell 5.1.
#>

param(
    [string]$BaseUrl = "http://localhost:5026",
    [string]$TenantSlug = "ferreteria-don-pedro",
    [string]$Email = "admin@ferreteriadonpedro.com",
    [string]$Password = "Admin1234"
)

$ErrorActionPreference = "Stop"

$script:Token = $null
$script:SupplierId = $null
$script:CategoryId = $null
$script:UnitId = $null
$script:ProductId = $null
$script:PurchaseOrderId = $null
$script:OrderItemId = $null

$script:Results = [ordered]@{
    Login              = $false
    CreateSupplier     = $false
    Setup              = $false
    CreateProduct      = $false
    CreatePO           = $false
    ReceivePartial     = $false
    CppAfterFirst      = $false
    ReceiveRest        = $false
    CppAfterSecond     = $false
    OrderStatus        = $false
    RegisterPayment    = $false
    AccountStatement   = $false
}

function Get-AuthHeaders {
    return @{
        "X-Tenant-Slug" = $TenantSlug
        "Authorization" = "Bearer $script:Token"
    }
}

function Write-ErrorDetails {
    param([Parameter(Mandatory)] $ErrorRecord, [Parameter(Mandatory)] [string]$Step)

    Write-Host "`n$([char]0x274C) ERROR en el paso: $Step" -ForegroundColor Red
    $exception = $ErrorRecord.Exception

    if ($exception.Response) {
        try {
            $stream = $exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            Write-Host "Response Body: $($reader.ReadToEnd())" -ForegroundColor Red
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

function Show-Summary {
    Write-Host "`n=== RESUMEN MODULO PROVEEDORES ===" -ForegroundColor Cyan
    foreach ($key in $script:Results.Keys) {
        Write-Host "$(Get-StatusIcon $script:Results[$key]) $key"
    }
}

Write-Host "=== TEST PROVEEDORES / ORDENES DE COMPRA - AXON API ===" -ForegroundColor Cyan

# --- Login ---
$body = @{ email = $Email; password = $Password; tenantSlug = $TenantSlug } | ConvertTo-Json
try {
    $login = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" -Method Post -ContentType "application/json" `
        -Headers @{ "X-Tenant-Slug" = $TenantSlug } -Body $body -ErrorAction Stop
    $script:Token = $login.data.accessToken
    Write-Host "$([char]0x2705) Login exitoso" -ForegroundColor Green
    $script:Results.Login = $true
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Login"
    Show-Summary; Read-Host "Presiona Enter"; exit 1
}

# --- Crear proveedor ---
try {
    $body = @{
        name            = "Distribuidora Ferretera SAS"
        nit             = "800.555.111-2"
        contactName     = "Marcela Gomez"
        phone           = "310 555 1234"
        email           = "ventas@distribuidoraferretera.com"
        address         = "Av. 68 #25-10"
        city            = "Bogota"
        paymentTermDays = 30
    } | ConvertTo-Json

    $result = Invoke-RestMethod -Uri "$BaseUrl/api/suppliers" -Method Post -ContentType "application/json" `
        -Headers (Get-AuthHeaders) -Body $body -ErrorAction Stop

    $script:SupplierId = $result.data
    Write-Host "$([char]0x2705) Proveedor creado: $script:SupplierId" -ForegroundColor Green
    $script:Results.CreateSupplier = $true
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Crear proveedor"
    Show-Summary; Read-Host "Presiona Enter"; exit 1
}

# --- Setup: reutilizar unidad, crear categoria propia ---
try {
    $units = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/units" -Method Get -Headers (Get-AuthHeaders) -ErrorAction Stop
    $script:UnitId = ($units.data | Where-Object { $_.abbreviation -eq "und" } | Select-Object -First 1).id

    $catBody = @{ name = "Insumos Proveedor Test"; description = "Categoria para test de proveedores" } | ConvertTo-Json
    $cat = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/categories" -Method Post -ContentType "application/json" `
        -Headers (Get-AuthHeaders) -Body $catBody -ErrorAction Stop
    $script:CategoryId = $cat.data

    Write-Host "$([char]0x2705) Setup: unit=$script:UnitId categoria=$script:CategoryId" -ForegroundColor Green
    $script:Results.Setup = $true
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Setup (unidad/categoria)"
    Show-Summary; Read-Host "Presiona Enter"; exit 1
}

# --- Crear producto con stock 0 y costo inicial 0 (para validar CPP desde cero) ---
try {
    $prodBody = @{
        sku         = "CPP-TEST-001"
        name        = "Producto Test CPP"
        description = "Producto para validar costo promedio ponderado"
        price       = 10000
        cost        = 1
        minStock    = 0
        categoryId  = $script:CategoryId
        unitId      = $script:UnitId
        attributes  = $null
    } | ConvertTo-Json

    $prod = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/products" -Method Post -ContentType "application/json" `
        -Headers (Get-AuthHeaders) -Body $prodBody -ErrorAction Stop

    $script:ProductId = $prod.data
    Write-Host "$([char]0x2705) Producto creado (stock 0): $script:ProductId" -ForegroundColor Green
    $script:Results.CreateProduct = $true
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Crear producto"
    Show-Summary; Read-Host "Presiona Enter"; exit 1
}

# --- Crear orden de compra: 20 unidades a $1000 ---
try {
    $poBody = @{
        supplierId   = $script:SupplierId
        items        = @(@{ productId = $script:ProductId; quantityOrdered = 20; unitCost = 1000 })
        expectedDate = $null
        notes        = "OC de prueba para validar CPP"
    } | ConvertTo-Json -Depth 5

    $po = Invoke-RestMethod -Uri "$BaseUrl/api/suppliers/purchase-orders" -Method Post -ContentType "application/json" `
        -Headers (Get-AuthHeaders) -Body $poBody -ErrorAction Stop

    $script:PurchaseOrderId = $po.data
    Write-Host "$([char]0x2705) Orden de compra creada: $script:PurchaseOrderId (20 uds @ `$1000)" -ForegroundColor Green
    $script:Results.CreatePO = $true
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Crear orden de compra"
    Show-Summary; Read-Host "Presiona Enter"; exit 1
}

# --- Obtener el Id real del item de la orden (lo genera el backend) ---
try {
    $orders = Invoke-RestMethod -Uri "$BaseUrl/api/suppliers/purchase-orders?supplierId=$script:SupplierId&pageSize=5" `
        -Method Get -Headers (Get-AuthHeaders) -ErrorAction Stop
    Write-Host "   (ordenes del proveedor: $($orders.data.totalCount))" -ForegroundColor DarkGray
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Listar ordenes de compra"
}

# ---------- Recepcion 1: 10 unidades a $1000 (stock actual 0 -> CPP debe ser 1000) ----------
try {
    # PurchaseOrderItemId no se expone en GetPurchaseOrdersQuery; se obtiene via SQL directo
    # porque no hay endpoint GetPurchaseOrderById todavia (gap conocido, no bloqueante aqui).
    $itemId = docker exec axon_postgres psql -U axon_user -d axon_master -t -A -c `
        "SELECT id FROM tenant_feaad01d.purchase_order_items WHERE purchase_order_id = '$script:PurchaseOrderId' LIMIT 1;"
    $script:OrderItemId = $itemId.Trim()

    $recvBody = @{
        items = @(@{ purchaseOrderItemId = $script:OrderItemId; quantityReceived = 10 })
        notes = "Primera recepcion parcial"
    } | ConvertTo-Json -Depth 5

    $recv = Invoke-RestMethod -Uri "$BaseUrl/api/suppliers/purchase-orders/$script:PurchaseOrderId/receive" `
        -Method Post -ContentType "application/json" -Headers (Get-AuthHeaders) -Body $recvBody -ErrorAction Stop

    Write-Host "$([char]0x2705) Recepcion 1: $($recv.data.totalReceived) - Estado orden: $($recv.data.orderStatus)" -ForegroundColor Green
    $script:Results.ReceivePartial = $true

    if ($recv.data.orderStatus -eq "PartiallyReceived") {
        Write-Host "$([char]0x2705) Estado correcto tras recepcion parcial: PartiallyReceived" -ForegroundColor Green
    } else {
        Write-Host "$([char]0x274C) Estado inesperado: $($recv.data.orderStatus) (se esperaba PartiallyReceived)" -ForegroundColor Red
    }
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Recepcion parcial 1"
    Show-Summary; Read-Host "Presiona Enter"; exit 1
}

# Verificar CPP: stock 0 + 10 @ 1000 => CPP = 1000
try {
    $product = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/products/$script:ProductId" -Method Get `
        -Headers (Get-AuthHeaders) -ErrorAction Stop

    Write-Host "   Producto tras recepcion 1: stock=$($product.data.stock) cost=$($product.data.cost)" -ForegroundColor DarkGray

    if ($product.data.stock -eq 10 -and $product.data.cost -eq 1000) {
        Write-Host "$([char]0x2705) CPP correcto tras 1ra recepcion: stock=10, cost=1000" -ForegroundColor Green
        $script:Results.CppAfterFirst = $true
    } else {
        Write-Host "$([char]0x274C) CPP incorrecto: stock=$($product.data.stock) cost=$($product.data.cost) (esperado stock=10 cost=1000)" -ForegroundColor Red
    }
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Verificar CPP tras recepcion 1"
}

# ---------- Recepcion 2: los 10 restantes a $1000 (mismo costo, para completar la orden) ----------
try {
    $recvBody = @{
        items = @(@{ purchaseOrderItemId = $script:OrderItemId; quantityReceived = 10 })
        notes = "Segunda recepcion - completa la orden"
    } | ConvertTo-Json -Depth 5

    $recv = Invoke-RestMethod -Uri "$BaseUrl/api/suppliers/purchase-orders/$script:PurchaseOrderId/receive" `
        -Method Post -ContentType "application/json" -Headers (Get-AuthHeaders) -Body $recvBody -ErrorAction Stop

    Write-Host "$([char]0x2705) Recepcion 2: $($recv.data.totalReceived) - Estado orden: $($recv.data.orderStatus)" -ForegroundColor Green
    $script:Results.ReceiveRest = $true

    if ($recv.data.orderStatus -eq "Received") {
        $script:Results.OrderStatus = $true
        Write-Host "$([char]0x2705) Estado correcto tras completar recepcion: Received" -ForegroundColor Green
    } else {
        Write-Host "$([char]0x274C) Estado inesperado: $($recv.data.orderStatus) (se esperaba Received)" -ForegroundColor Red
    }
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Recepcion 2 (resto)"
}

# Verificar CPP final: stock 10@1000 + 10@1000 = 20 @ 1000 (mismo costo, control de sanidad)
try {
    $product = Invoke-RestMethod -Uri "$BaseUrl/api/inventory/products/$script:ProductId" -Method Get `
        -Headers (Get-AuthHeaders) -ErrorAction Stop

    Write-Host "   Producto tras recepcion 2: stock=$($product.data.stock) cost=$($product.data.cost)" -ForegroundColor DarkGray

    if ($product.data.stock -eq 20 -and $product.data.cost -eq 1000) {
        Write-Host "$([char]0x2705) CPP correcto tras 2da recepcion: stock=20, cost=1000" -ForegroundColor Green
        $script:Results.CppAfterSecond = $true
    } else {
        Write-Host "$([char]0x274C) CPP incorrecto: stock=$($product.data.stock) cost=$($product.data.cost) (esperado stock=20 cost=1000)" -ForegroundColor Red
    }
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Verificar CPP tras recepcion 2"
}

# --- Registrar pago parcial al proveedor ---
try {
    $payBody = @{
        amount        = 15000
        paymentMethod = "Transfer"
        reference     = "TRX-TEST-001"
        notes         = "Abono parcial"
    } | ConvertTo-Json

    $pay = Invoke-RestMethod -Uri "$BaseUrl/api/suppliers/$script:SupplierId/payments" -Method Post -ContentType "application/json" `
        -Headers (Get-AuthHeaders) -Body $payBody -ErrorAction Stop

    Write-Host "$([char]0x2705) Pago registrado: $($pay.data)" -ForegroundColor Green
    $script:Results.RegisterPayment = $true
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Registrar pago a proveedor"
}

# --- Extracto de cuenta: recibido 20000 (20 uds @ 1000), pagado 15000 => balance 5000 ---
try {
    $statement = Invoke-RestMethod -Uri "$BaseUrl/api/suppliers/$script:SupplierId/account-statement" -Method Get `
        -Headers (Get-AuthHeaders) -ErrorAction Stop

    Write-Host "   Total comprado: $($statement.data.totalPurchased)  Total pagado: $($statement.data.totalPaid)  Balance: $($statement.data.balance)" -ForegroundColor DarkGray

    if ($statement.data.totalPurchased -eq 20000 -and $statement.data.totalPaid -eq 15000 -and $statement.data.balance -eq 5000) {
        Write-Host "$([char]0x2705) Extracto de cuenta correcto: balance=5000" -ForegroundColor Green
        $script:Results.AccountStatement = $true
    } else {
        Write-Host "$([char]0x274C) Extracto incorrecto (ver valores arriba, esperado purchased=20000 paid=15000 balance=5000)" -ForegroundColor Red
    }
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Extracto de cuenta"
}

Show-Summary
Read-Host "`nPresiona Enter para cerrar"
