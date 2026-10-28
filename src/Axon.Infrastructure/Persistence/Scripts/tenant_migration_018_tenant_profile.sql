-- Migración 018: datos comerciales del perfil del establecimiento
-- El portal web edita el perfil del negocio (GET/PUT /api/tenants/profile) con
-- campos que tenant_config no tenía: razón social, ciudad, departamento y el
-- régimen tributario en texto. tax_regime es la versión legible de
-- is_responsable_iva (que sigue siendo la fuente para facturación): el backend
-- mantiene ambos sincronizados al guardar.
--
-- Es IDEMPOTENTE: ADD COLUMN IF NOT EXISTS, y el backfill de tax_regime solo
-- toca filas que todavía tienen el default.
--
-- Aplicar a tenants existentes con:
--   .\scripts\migrate-tax-model.ps1 -MigrationSqlPath ..\src\Axon.Infrastructure\Persistence\Scripts\tenant_migration_018_tenant_profile.sql

ALTER TABLE {SCHEMA_NAME}.tenant_config
    ADD COLUMN IF NOT EXISTS legal_name VARCHAR(200),
    ADD COLUMN IF NOT EXISTS city VARCHAR(100),
    ADD COLUMN IF NOT EXISTS state VARCHAR(100),
    ADD COLUMN IF NOT EXISTS tax_regime VARCHAR(100) NOT NULL DEFAULT 'No responsable de IVA';

UPDATE {SCHEMA_NAME}.tenant_config
SET tax_regime = 'Responsable de IVA'
WHERE is_responsable_iva = true AND tax_regime = 'No responsable de IVA';
