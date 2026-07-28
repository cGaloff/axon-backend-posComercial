-- Migración 010: autorización de supervisor por PIN para anular/devolver
-- ventas (Matriz de Roles y Permisos v2, rol Cajero). El Cajero SÍ puede pedir
-- la anulación/devolución, pero requiere el PIN de un Administrador/Propietario
-- que quede registrado como autorizante (sales.authorized_by).
--
-- Es IDEMPOTENTE: columnas con IF NOT EXISTS.

ALTER TABLE {SCHEMA_NAME}.users
    ADD COLUMN IF NOT EXISTS pin_hash VARCHAR(500);

ALTER TABLE {SCHEMA_NAME}.sales
    ADD COLUMN IF NOT EXISTS authorized_by UUID;
