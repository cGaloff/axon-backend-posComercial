<#
    Flujo de humo del modulo de Usuarios: login -> listar roles -> crear
    usuario (Cajero) -> login con el usuario nuevo -> listar usuarios ->
    actualizar rol -> resetear contraseña -> desactivar -> verificar login
    bloqueado -> reactivar -> verificar que no se puede autodesactivar.

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
$script:OwnerId = $null
$script:CajeroRoleId = $null
$script:BodegueroRoleId = $null
$script:NewUserId = $null
$script:NewUserEmail = "cajero.test@ferreteriadonpedro.com"

$script:Results = [ordered]@{
    Login                = $false
    GetRoles             = $false
    CreateUser           = $false
    LoginNewUser         = $false
    GetUsers             = $false
    UpdateUserRole       = $false
    ResetPassword         = $false
    LoginAfterReset       = $false
    DeactivateUser        = $false
    LoginBlockedInactive  = $false
    ReactivateUser         = $false
    SelfDeactivateBlocked  = $false
}

function Get-AuthHeaders {
    param([string]$BearerToken = $script:Token)
    return @{ "X-Tenant-Slug" = $TenantSlug; "Authorization" = "Bearer $BearerToken" }
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
        } catch { Write-Host "No se pudo leer el body del error." -ForegroundColor Red }
    } else {
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
    Write-Host "`n=== RESUMEN MODULO USUARIOS ===" -ForegroundColor Cyan
    foreach ($key in $script:Results.Keys) {
        Write-Host "$(Get-StatusIcon $script:Results[$key]) $key"
    }
}

function Invoke-LoginAs {
    param([string]$LoginEmail, [string]$LoginPassword)

    $body = @{ email = $LoginEmail; password = $LoginPassword; tenantSlug = $TenantSlug } | ConvertTo-Json
    return Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" -Method Post -ContentType "application/json" `
        -Headers @{ "X-Tenant-Slug" = $TenantSlug } -Body $body -ErrorAction Stop
}

Write-Host "=== TEST USUARIOS - AXON API ===" -ForegroundColor Cyan

# --- Login owner ---
try {
    $login = Invoke-LoginAs -LoginEmail $Email -LoginPassword $Password
    $script:Token = $login.data.accessToken
    $script:OwnerId = ($login.data.accessToken -split '\.')[1]
    Write-Host "$([char]0x2705) Login exitoso (owner)" -ForegroundColor Green
    $script:Results.Login = $true
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Login owner"
    Show-Summary; Read-Host "Presiona Enter"; exit 1
}

# --- Listar roles ---
try {
    $roles = Invoke-RestMethod -Uri "$BaseUrl/api/users/roles" -Method Get -Headers (Get-AuthHeaders) -ErrorAction Stop
    $script:CajeroRoleId = ($roles.data | Where-Object { $_.name -eq "Cajero" } | Select-Object -First 1).id
    $script:BodegueroRoleId = ($roles.data | Where-Object { $_.name -eq "Bodeguero" } | Select-Object -First 1).id

    if (-not $script:CajeroRoleId -or -not $script:BodegueroRoleId) {
        throw "No se encontraron los roles 'Cajero'/'Bodeguero' esperados del seed."
    }

    Write-Host "$([char]0x2705) Roles obtenidos: $($roles.data.Count) (Cajero=$script:CajeroRoleId)" -ForegroundColor Green
    $script:Results.GetRoles = $true
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Listar roles"
    Show-Summary; Read-Host "Presiona Enter"; exit 1
}

# --- Crear usuario (Cajero) ---
try {
    $body = @{
        fullName = "Cajero de Prueba"
        email    = $script:NewUserEmail
        password = "Cajero1234"
        roleId   = $script:CajeroRoleId
    } | ConvertTo-Json

    $result = Invoke-RestMethod -Uri "$BaseUrl/api/users" -Method Post -ContentType "application/json" `
        -Headers (Get-AuthHeaders) -Body $body -ErrorAction Stop

    $script:NewUserId = $result.data
    Write-Host "$([char]0x2705) Usuario creado: $script:NewUserId" -ForegroundColor Green
    $script:Results.CreateUser = $true
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Crear usuario"
    Show-Summary; Read-Host "Presiona Enter"; exit 1
}

# --- Login con el usuario nuevo ---
try {
    $newLogin = Invoke-LoginAs -LoginEmail $script:NewUserEmail -LoginPassword "Cajero1234"
    Write-Host "$([char]0x2705) Login con usuario nuevo exitoso (rol Cajero funcionando)" -ForegroundColor Green
    $script:Results.LoginNewUser = $true
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Login con usuario nuevo"
}

# --- Listar usuarios ---
try {
    $users = Invoke-RestMethod -Uri "$BaseUrl/api/users" -Method Get -Headers (Get-AuthHeaders) -ErrorAction Stop
    $found = $users.data | Where-Object { $_.id -eq $script:NewUserId }

    if (-not $found) { throw "El usuario creado no aparece en el listado." }

    Write-Host "$([char]0x2705) Usuarios listados: $($users.data.Count), usuario nuevo presente con rol '$($found.roleName)'" -ForegroundColor Green
    $script:Results.GetUsers = $true
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Listar usuarios"
}

# --- Actualizar rol a Bodeguero ---
try {
    $body = @{ fullName = "Cajero de Prueba (ahora Bodeguero)"; roleId = $script:BodegueroRoleId } | ConvertTo-Json

    Invoke-RestMethod -Uri "$BaseUrl/api/users/$script:NewUserId" -Method Put -ContentType "application/json" `
        -Headers (Get-AuthHeaders) -Body $body -ErrorAction Stop | Out-Null

    Write-Host "$([char]0x2705) Usuario actualizado a rol Bodeguero" -ForegroundColor Green
    $script:Results.UpdateUserRole = $true
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Actualizar usuario"
}

# --- Resetear contraseña ---
try {
    $body = @{ newPassword = "NuevaClave1234" } | ConvertTo-Json

    Invoke-RestMethod -Uri "$BaseUrl/api/users/$script:NewUserId/reset-password" -Method Post -ContentType "application/json" `
        -Headers (Get-AuthHeaders) -Body $body -ErrorAction Stop | Out-Null

    Write-Host "$([char]0x2705) Contraseña reseteada" -ForegroundColor Green
    $script:Results.ResetPassword = $true
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Resetear contraseña"
}

# --- Login con la nueva contraseña ---
try {
    Invoke-LoginAs -LoginEmail $script:NewUserEmail -LoginPassword "NuevaClave1234" | Out-Null
    Write-Host "$([char]0x2705) Login con contraseña reseteada exitoso" -ForegroundColor Green
    $script:Results.LoginAfterReset = $true
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Login con contraseña reseteada"
}

# --- Desactivar usuario ---
try {
    Invoke-RestMethod -Uri "$BaseUrl/api/users/$script:NewUserId" -Method Delete -Headers (Get-AuthHeaders) -ErrorAction Stop | Out-Null
    Write-Host "$([char]0x2705) Usuario desactivado" -ForegroundColor Green
    $script:Results.DeactivateUser = $true
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Desactivar usuario"
}

# --- Verificar que el login del usuario inactivo queda bloqueado ---
try {
    Invoke-LoginAs -LoginEmail $script:NewUserEmail -LoginPassword "NuevaClave1234" | Out-Null
    Write-Host "$([char]0x274C) ERROR: el usuario desactivado pudo loguearse (se esperaba bloqueo)" -ForegroundColor Red
}
catch {
    $statusCode = Get-StatusCode $_
    if ($statusCode -eq 400) {
        Write-Host "$([char]0x2705) Login bloqueado correctamente para usuario inactivo (400)" -ForegroundColor Green
        $script:Results.LoginBlockedInactive = $true
    } else {
        Write-ErrorDetails -ErrorRecord $_ -Step "Verificar bloqueo de login (usuario inactivo)"
    }
}

# --- Reactivar usuario ---
try {
    Invoke-RestMethod -Uri "$BaseUrl/api/users/$script:NewUserId/reactivate" -Method Post -Headers (Get-AuthHeaders) -ErrorAction Stop | Out-Null
    Write-Host "$([char]0x2705) Usuario reactivado" -ForegroundColor Green
    $script:Results.ReactivateUser = $true
}
catch {
    Write-ErrorDetails -ErrorRecord $_ -Step "Reactivar usuario"
}

# --- Verificar que el owner no puede autodesactivarse ---
try {
    $meResponse = Invoke-RestMethod -Uri "$BaseUrl/api/users" -Method Get -Headers (Get-AuthHeaders) -ErrorAction Stop
    $owner = $meResponse.data | Where-Object { $_.email -eq $Email } | Select-Object -First 1

    Invoke-RestMethod -Uri "$BaseUrl/api/users/$($owner.id)" -Method Delete -Headers (Get-AuthHeaders) -ErrorAction Stop | Out-Null
    Write-Host "$([char]0x274C) ERROR: el owner pudo autodesactivarse (se esperaba bloqueo)" -ForegroundColor Red
}
catch {
    $statusCode = Get-StatusCode $_
    if ($statusCode -eq 400) {
        Write-Host "$([char]0x2705) Autodesactivacion bloqueada correctamente (400)" -ForegroundColor Green
        $script:Results.SelfDeactivateBlocked = $true
    } else {
        Write-ErrorDetails -ErrorRecord $_ -Step "Verificar bloqueo de autodesactivacion"
    }
}

Show-Summary
Read-Host "`nPresiona Enter para cerrar"
