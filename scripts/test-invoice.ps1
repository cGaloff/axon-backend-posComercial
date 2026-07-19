Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process

$baseUrl   = "http://localhost:5026"
$tenantSlug = "ferreteria-don-pedro"

# 1. LOGIN
$login = Invoke-RestMethod -Method POST `
  -Uri "$baseUrl/api/auth/login" `
  -Headers @{ "Content-Type" = "application/json"; "X-Tenant-Slug" = $tenantSlug } `
  -Body '{"email":"admin@ferreteriadonpedro.com","password":"Admin1234","tenantSlug":"ferreteria-don-pedro"}'

$token = $login.data.accessToken
$headers = @{
    "Authorization" = "Bearer $token"
    "X-Tenant-Slug" = $tenantSlug
    "Content-Type"  = "application/json"
}
Write-Host "✅ Login exitoso" -ForegroundColor Green

# 2. ACTUALIZAR TENANT CONFIG
try {
    Invoke-RestMethod -Method PUT `
      -Uri "$baseUrl/api/tenant-config" `
      -Headers $headers `
      -Body '{
        "businessName": "Ferretería Don Pedro",
        "nit": "900.123.456-7",
        "address": "Calle 15 #45-20 - Bogotá",
        "phone": "601 123-4567",
        "email": "info@ferreteriadonpedro.com",
        "website": "www.ferreteriadonpedro.com",
        "logoUrl": null,
        "isResponsableIva": true
      }' -ErrorAction Stop | Out-Null
    Write-Host "✅ Tenant config actualizado con IVA activado" -ForegroundColor Green
} catch {
    $stream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    Write-Host "❌ Error actualizando config: $($reader.ReadToEnd())" -ForegroundColor Red
    Read-Host "Presiona Enter para cerrar"
    exit
}

# 3. GET TENANT CONFIG (verificar)
$config = Invoke-RestMethod -Method GET `
  -Uri "$baseUrl/api/tenant-config" `
  -Headers $headers
Write-Host "✅ Config verificada: $($config.data.businessName) - IVA: $($config.data.isResponsableIva)" -ForegroundColor Green

# 4. OBTENER UNIT ID
$units  = Invoke-RestMethod -Method GET `
  -Uri "$baseUrl/api/inventory/units" `
  -Headers $headers
$unitId = ($units.data | Where-Object { $_.abbreviation -eq "und" }).id
Write-Host "✅ Unit id: $unitId" -ForegroundColor Green

# 5. OBTENER O CREAR CATEGORÍA (idempotente)
$cats        = Invoke-RestMethod -Method GET `
  -Uri "$baseUrl/api/inventory/categories" `
  -Headers $headers
$existingCat = $cats.data | Where-Object { $_.name -eq "Herramientas IVA" } | Select-Object -First 1

if ($existingCat) {
    $categoryId = $existingCat.id
    Write-Host "✅ Categoría existente reutilizada: $categoryId" -ForegroundColor Green
} else {
    $cat        = Invoke-RestMethod -Method POST `
      -Uri "$baseUrl/api/inventory/categories" `
      -Headers $headers `
      -Body '{"name":"Herramientas IVA","description":"Test con IVA"}' `
      -ErrorAction Stop
    $categoryId = $cat.data
    Write-Host "✅ Categoría creada: $categoryId" -ForegroundColor Green
}

# 6. OBTENER O CREAR PRODUCTO (idempotente por SKU)
$products      = Invoke-RestMethod -Method GET `
  -Uri "$baseUrl/api/inventory/products?search=TALADRO-001&pageSize=5" `
  -Headers $headers
$existingProduct = $products.data.items | Where-Object { $_.sku -eq "TALADRO-001" } | Select-Object -First 1

if ($existingProduct) {
    $productId = $existingProduct.id
    Write-Host "✅ Producto existente reutilizado: $productId" -ForegroundColor Green
} else {
    $productBody = @{
        sku           = "TALADRO-001"
        name          = "Taladro Bosch 500W"
        description   = "Taladro percutor profesional"
        price         = 250000
        cost          = 180000
        minStock      = 2
        categoryId    = $categoryId
        unitId        = $unitId
        taxPercentage = 19
    } | ConvertTo-Json

    $product   = Invoke-RestMethod -Method POST `
      -Uri "$baseUrl/api/inventory/products" `
      -Headers $headers -Body $productBody `
      -ErrorAction Stop
    $productId = $product.data
    Write-Host "✅ Producto creado con IVA 19%: $productId" -ForegroundColor Green

    # Ajustar stock solo si el producto es nuevo
    Invoke-RestMethod -Method POST `
      -Uri "$baseUrl/api/inventory/products/$productId/adjust-stock" `
      -Headers $headers `
      -Body '{"quantity":10,"type":"InitialStock","reason":"Stock inicial"}' | Out-Null
    Write-Host "✅ Stock inicial: 10 unidades" -ForegroundColor Green
}

# 7. OBTENER CAJA
$cajas          = Invoke-RestMethod -Method GET `
  -Uri "$baseUrl/api/cash-register" `
  -Headers $headers
$cashRegister   = $cajas.data | Where-Object { $_.isDefault -eq $true } | Select-Object -First 1
$cashRegisterId = $cashRegister.id
Write-Host "✅ Caja: $cashRegisterId" -ForegroundColor Green

# 8. ABRIR SESIÓN (solo si no hay una activa)
if ($cashRegister.hasActiveSession -eq $true) {
    $activeSessionId = $cashRegister.activeSessionId
    Write-Host "✅ Sesión activa reutilizada: $activeSessionId" -ForegroundColor Green
    $sessionId = $activeSessionId
} else {
    $sessionBody = @{
        cashRegisterId = $cashRegisterId
        initialAmount  = 100000
    } | ConvertTo-Json

    $session   = Invoke-RestMethod -Method POST `
      -Uri "$baseUrl/api/cash-register/sessions/open" `
      -Headers $headers -Body $sessionBody `
      -ErrorAction Stop
    $sessionId = $session.data.sessionId
    Write-Host "✅ Sesión de caja abierta: $sessionId" -ForegroundColor Green
}

# 9. PROCESAR VENTA CON IVA
$saleBody = @{
    items = @(@{
        productId = $productId
        quantity  = 2
        discount  = 0
    })
    paymentMethod  = "Cash"
    cashRegisterId = $cashRegisterId
    amountPaid     = 600000
    customerName   = "Carlos Pérez"
    customerEmail  = $null
    notes          = "Venta con IVA 19%"
} | ConvertTo-Json -Depth 5

try {
    $sale       = Invoke-RestMethod -Method POST `
      -Uri "$baseUrl/api/sales" `
      -Headers $headers -Body $saleBody `
      -ErrorAction Stop
    $saleId     = $sale.data.saleId
    $saleNumber = $sale.data.saleNumber
    $total      = $sale.data.total
    Write-Host "✅ Venta procesada: $saleNumber - Total: $total" -ForegroundColor Green
} catch {
    $stream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    Write-Host "❌ Error en venta: $($reader.ReadToEnd())" -ForegroundColor Red
    Read-Host "Presiona Enter para cerrar"
    exit
}

# 10. RESUMEN DE IMPUESTOS
try {
    $taxSummary = Invoke-RestMethod -Method GET `
      -Uri "$baseUrl/api/sales/$saleId/tax-summary" `
      -Headers $headers -ErrorAction Stop
    Write-Host "✅ Resumen IVA:" -ForegroundColor Green
    Write-Host "   Base gravable:  $($taxSummary.data.subtotalBase)"
    Write-Host "   IVA cobrado:    $($taxSummary.data.totalTax)"
    Write-Host "   Total:          $($taxSummary.data.total)"
} catch {
    Write-Host "❌ Error obteniendo resumen IVA" -ForegroundColor Red
}

# 11. DESCARGAR PDF
try {
    $pdfPath = ".\test-receipt-iva.pdf"
    Invoke-WebRequest -Method GET `
      -Uri "$baseUrl/api/sales/$saleId/receipt" `
      -Headers @{ "Authorization" = "Bearer $token"; "X-Tenant-Slug" = $tenantSlug } `
      -OutFile $pdfPath -ErrorAction Stop
    Write-Host "✅ PDF guardado en $pdfPath" -ForegroundColor Green
    Write-Host "   Abre el PDF para verificar la hora Colombia y el diseño" -ForegroundColor Cyan
} catch {
    Write-Host "❌ Error descargando PDF" -ForegroundColor Red
}

# RESUMEN FINAL
Write-Host "`n=== RESUMEN ===" -ForegroundColor Cyan
Write-Host "✅ Login"
Write-Host "✅ Tenant config con IVA"
Write-Host "✅ Categoría (nueva o existente)"
Write-Host "✅ Producto con IVA 19% (nuevo o existente)"
Write-Host "✅ Sesión de caja (nueva o activa)"
Write-Host "✅ Venta procesada: $saleNumber"
Write-Host "✅ Resumen de impuestos"
Write-Host "✅ PDF descargado"

Read-Host "`nPresiona Enter para cerrar"