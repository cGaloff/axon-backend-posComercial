-- Migración 012: bloqueo por intentos fallidos de login, cierre de sesión por
-- inactividad, e historial de intentos de login (Matriz de Roles y Permisos
-- v2, "seguridad técnica" — sin 2FA, descartado explícitamente para esta
-- versión).
--
-- Es IDEMPOTENTE: columnas y tabla con IF NOT EXISTS.

ALTER TABLE {SCHEMA_NAME}.users
    ADD COLUMN IF NOT EXISTS failed_login_attempts INT NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS locked_until TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS last_activity_at TIMESTAMPTZ;

CREATE TABLE IF NOT EXISTS {SCHEMA_NAME}.login_attempts (
    id UUID PRIMARY KEY,
    email VARCHAR(200) NOT NULL,
    user_id UUID,
    success BOOLEAN NOT NULL,
    ip_address VARCHAR(64),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_login_attempts_created_user ON {SCHEMA_NAME}.login_attempts (created_at, user_id);
