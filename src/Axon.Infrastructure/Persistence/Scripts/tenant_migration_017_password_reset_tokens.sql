-- Migración 017: tabla de tokens de recuperación de contraseña
-- Soporta el flujo forgot-password/reset-password: mismo patrón que
-- refresh_tokens (valor crudo enviado al usuario, solo el hash SHA-256
-- persistido en token_hash), pero con used_at en vez de revoked_at, porque un
-- token de reset se "usa" una sola vez, no se "revoca".
--
-- Es IDEMPOTENTE: CREATE TABLE IF NOT EXISTS, no requiere backfill (tabla
-- nueva, sin filas previas que migrar).

CREATE TABLE IF NOT EXISTS {SCHEMA_NAME}.password_reset_tokens (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.users(id) ON DELETE CASCADE,
    token_hash VARCHAR(64) NOT NULL UNIQUE,
    expires_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    used_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_password_reset_tokens_user ON {SCHEMA_NAME}.password_reset_tokens (user_id);
