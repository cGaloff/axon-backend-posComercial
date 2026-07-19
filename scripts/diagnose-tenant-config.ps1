<#
    Diagnostico puntual para el error de PUT /api/tenant-config.
    No asume que el fallo es el body: imprime el token, el status code real
    y el cuerpo crudo de la respuesta de error para descartar auth/tenant
    antes de mirar el JSON.
#>

param(
    [string]$BaseUrl = "http://localhost:5026",
    [string]$TenantSlug = "ferreteria-don-pedro",
    [string]$Email = "admin@ferreteriadonpedro.com",
    [string]$Password = "Admin1234"
)

$ErrorActionPreference = "Stop"

Write-Host "--- LOGIN ---"

$loginBody = '{"email":"' + $Email + '","password":"' + $Password + '","tenantSlug":"' + $TenantSlug + '"}'

$login = Invoke-RestMethod -Method POST `
    -Uri "$BaseUrl/api/auth/login" `
    -Headers @{ "Content-Type" = "application/json"; "X-Tenant-Slug" = $TenantSlug } `
    -Body $loginBody

$token = $login.data.accessToken

Write-Host "Token recibido: $(if ([string]::IsNullOrWhiteSpace($token)) { 'VACIO/NULL <-- este es el problema si sale esto' } else { $token.Substring(0, [Math]::Min(20, $token.Length)) + '...' })"

$headers = @{
    "Authorization" = "Bearer $token"
    "X-Tenant-Slug" = $TenantSlug
    "Content-Type"  = "application/json"
}

Write-Host "`n--- GET /api/tenant-config (sin body, para descartar auth/tenant) ---"

try {
    $getResult = Invoke-RestMethod -Method GET -Uri "$BaseUrl/api/tenant-config" -Headers $headers
    Write-Host "GET OK:"
    $getResult | ConvertTo-Json -Depth 5 | Write-Host
}
catch {
    Write-Host "GET FALLO. Status: $([int]$_.Exception.Response.StatusCode)"
    $stream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    Write-Host "Body crudo: $($reader.ReadToEnd())"
}

Write-Host "`n--- PUT /api/tenant-config ---"

$putBody = '{
    "businessName": "Ferretería Don Pedro",
    "nit": "900.123.456-7",
    "address": "Calle 15 #45-20 - Bogotá",
    "phone": "601 123-4567",
    "email": "info@ferreteriadonpedro.com",
    "website": "www.ferreteriadonpedro.com",
    "logoUrl": null,
    "isResponsableIva": true
}'

# Bytes exactos que se van a mandar, para descartar problemas de encoding del lado cliente.
$bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($putBody)
Write-Host "Bytes del body (primeros 60): $(($bodyBytes | Select-Object -First 60 | ForEach-Object { $_.ToString('X2') }) -join ' ')"

try {
    $putResult = Invoke-RestMethod -Method PUT -Uri "$BaseUrl/api/tenant-config" -Headers $headers -Body $putBody
    Write-Host "PUT OK:"
    $putResult | ConvertTo-Json -Depth 5 | Write-Host
}
catch {
    Write-Host "PUT FALLO. Status: $([int]$_.Exception.Response.StatusCode)"
    $stream = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    Write-Host "Body crudo completo del error: $($reader.ReadToEnd())"
}

Read-Host "`nPresiona Enter para cerrar"
