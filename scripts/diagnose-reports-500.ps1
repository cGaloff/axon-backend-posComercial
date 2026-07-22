<#
    Diagnostico puntual para los 500 reportados en:
    GET /api/reports/cash-flow, /api/reports/sales-summary, /api/cash-register/sessions

    A diferencia de un try/catch normal, este script lee el CUERPO COMPLETO
    de la respuesta de error (en Development, ASP.NET Core suele incluir el
    stack trace real ahi, no solo "Internal Server Error").
#>

param(
    [string]$BaseUrl = "http://localhost:5026",
    [string]$TenantSlug = "ferreteria-don-pedro",
    [string]$Email = "admin@ferreteriadonpedro.com",
    [string]$Password = "Admin1234"
)

$ErrorActionPreference = "Stop"

function Write-FullError {
    param($ErrorRecord, [string]$Step)

    Write-Host "`n$([char]0x274C) $Step" -ForegroundColor Red

    if ($ErrorRecord.Exception.Response) {
        try {
            $status = [int]$ErrorRecord.Exception.Response.StatusCode
            Write-Host "Status: $status" -ForegroundColor Red
        } catch {}

        try {
            $stream = $ErrorRecord.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $body = $reader.ReadToEnd()
            Write-Host "--- BODY COMPLETO ---" -ForegroundColor Yellow
            Write-Host $body
            Write-Host "--- FIN BODY ---" -ForegroundColor Yellow
        } catch {
            Write-Host "No se pudo leer el body." -ForegroundColor Red
        }
    } else {
        Write-Host "Excepcion sin respuesta HTTP: $($ErrorRecord.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "=== DIAGNOSTICO: BaseUrl=$BaseUrl Tenant=$TenantSlug ===" -ForegroundColor Cyan

# 1. Login
$loginBody = @{ email = $Email; password = $Password; tenantSlug = $TenantSlug } | ConvertTo-Json
try {
    $login = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" -Method Post -ContentType "application/json" `
        -Headers @{ "X-Tenant-Slug" = $TenantSlug } -Body $loginBody -ErrorAction Stop
    $token = $login.data.accessToken
    Write-Host "$([char]0x2705) Login OK" -ForegroundColor Green
} catch {
    Write-FullError -ErrorRecord $_ -Step "LOGIN"
    Read-Host "Presiona Enter para cerrar"
    exit 1
}

$headers = @{ "Authorization" = "Bearer $token"; "X-Tenant-Slug" = $TenantSlug }

# 2. Confirmar que las tablas nuevas existen (via los propios endpoints, sin psql)
Write-Host "`n--- Chequeo rapido de modulos relacionados ---"
try {
    $suppliers = Invoke-RestMethod -Uri "$BaseUrl/api/suppliers" -Method Get -Headers $headers -ErrorAction Stop
    Write-Host "$([char]0x2705) /api/suppliers responde (modulo Proveedores existe en este tenant)" -ForegroundColor Green
} catch {
    Write-FullError -ErrorRecord $_ -Step "GET /api/suppliers (si esto tambien falla con 500, el tenant no tiene el modulo de Proveedores al dia)"
}

# 3. Los 3 endpoints reportados, con body completo del error si fallan
$from = "2026-01-01T00:00:00Z"
$to = "2026-12-31T23:59:59Z"

$endpoints = @(
    @{ Name = "cash-flow"; Url = "$BaseUrl/api/reports/cash-flow?fromDate=$from&toDate=$to&groupBy=Day" },
    @{ Name = "sales-summary"; Url = "$BaseUrl/api/reports/sales-summary?fromDate=$from&toDate=$to" },
    @{ Name = "cash-register/sessions"; Url = "$BaseUrl/api/cash-register/sessions" }
)

foreach ($ep in $endpoints) {
    Write-Host "`n--- $($ep.Name) ---"
    try {
        $r = Invoke-RestMethod -Uri $ep.Url -Method Get -Headers $headers -ErrorAction Stop
        Write-Host "$([char]0x2705) 200 OK" -ForegroundColor Green
    } catch {
        Write-FullError -ErrorRecord $_ -Step $ep.Name
    }
}

Read-Host "`nPresiona Enter para cerrar (copia y pasame todo lo de arriba, en especial los BODY COMPLETO)"
