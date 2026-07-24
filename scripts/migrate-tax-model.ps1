<#
    Aplica la migracion 002 (modelo de impuestos flexible: TaxType/ProductTax/
    SaleItemTax) a TODOS los tenants ya aprovisionados, ejecutando
    tenant_migration_002_flexible_taxes.sql contra cada schema.

    No existe un sistema formal de version de schema por tenant en este
    proyecto (el esquema de cada tenant se crea con SQL embebido al
    aprovisionar, sin migraciones EF). Esta es la via operativa elegida para
    propagar el cambio a tenants existentes: un script de mantenimiento que un
    operador con acceso a Docker/DB corre manualmente, igual que los demas
    scripts de /scripts.

    Es IDEMPOTENTE: correrlo mas de una vez no falla ni duplica datos (ver
    comentarios del propio .sql). Tenants nuevos NO necesitan este script -
    tenant_schema_template.sql ya incluye las tablas nuevas desde el principio.

    NOTA: el runner (todo excepto el parametro por defecto de -MigrationSqlPath)
    es generico. Para aplicar OTRA migracion de mantenimiento (ej. la 003, pagos
    divididos) reutiliza este mismo script pasando:
        .\migrate-tax-model.ps1 -MigrationSqlPath ..\src\Axon.Infrastructure\Persistence\Scripts\tenant_migration_003_split_payments.sql
#>

param(
    [string]$PostgresContainer = "axon_postgres",
    [string]$PostgresUser = "axon_user",
    [string]$DatabaseName = "axon_master",
    [string]$MigrationSqlPath = (Join-Path $PSScriptRoot "..\src\Axon.Infrastructure\Persistence\Scripts\tenant_migration_002_flexible_taxes.sql")
)

$ErrorActionPreference = "Stop"

function Invoke-Psql {
    param([Parameter(Mandatory)] [string]$Sql)

    $output = docker exec -i $PostgresContainer psql -U $PostgresUser -d $DatabaseName -t -A -c $Sql 2>&1

    if ($LASTEXITCODE -ne 0) {
        throw "psql fallo (exit $LASTEXITCODE) contra la BD '$DatabaseName': $output"
    }

    return ($output | Out-String).Trim()
}

function Invoke-PsqlScript {
    param([Parameter(Mandatory)] [string]$Sql)

    $tempFile = [System.IO.Path]::GetTempFileName()

    try {
        Set-Content -Path $tempFile -Value $Sql -NoNewline

        Get-Content -Path $tempFile -Raw | docker exec -i $PostgresContainer psql -U $PostgresUser -d $DatabaseName -v ON_ERROR_STOP=1

        if ($LASTEXITCODE -ne 0) {
            throw "psql devolvio exit code $LASTEXITCODE"
        }
    }
    finally {
        Remove-Item -Path $tempFile -ErrorAction SilentlyContinue
    }
}

if (-not (Test-Path $MigrationSqlPath)) {
    throw "No se encontro el script de migracion en '$MigrationSqlPath'."
}

$migrationSqlTemplate = Get-Content -Path $MigrationSqlPath -Raw

Write-Host "=== Migracion 002: modelo de impuestos flexible ===" -ForegroundColor Cyan

$schemasRaw = Invoke-Psql -Sql "SELECT schema_name FROM public.tenants;"

if ([string]::IsNullOrWhiteSpace($schemasRaw)) {
    Write-Host "No se encontraron tenants en public.tenants (BD: $DatabaseName)." -ForegroundColor Yellow
    exit 0
}

$schemas = $schemasRaw -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" }

Write-Host "Tenants encontrados: $($schemas.Count)`n"

$failures = @()

foreach ($schema in $schemas) {
    Write-Host "--- $schema ---"

    try {
        $sql = $migrationSqlTemplate.Replace("{SCHEMA_NAME}", $schema)
        Invoke-PsqlScript -Sql $sql

        Write-Host "$([char]0x2705) Migrado" -ForegroundColor Green
    }
    catch {
        Write-Host "$([char]0x274C) Error: $($_.Exception.Message)" -ForegroundColor Red
        $failures += $schema
    }
}

Write-Host "`n=== RESUMEN ===" -ForegroundColor Cyan
Write-Host "Total: $($schemas.Count) | Exitosos: $($schemas.Count - $failures.Count) | Fallidos: $($failures.Count)"

if ($failures.Count -gt 0) {
    Write-Host "Tenants con error: $($failures -join ', ')" -ForegroundColor Red
    exit 1
}
