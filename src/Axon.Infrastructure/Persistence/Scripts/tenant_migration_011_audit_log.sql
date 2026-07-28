-- Migración 011: log de auditoría genérico (Matriz de Roles y Permisos v2,
-- regla transversal A y prerequisito de Habeas Data). Tabla inmutable — el
-- código de la aplicación nunca hace UPDATE/DELETE sobre ella, ni siquiera
-- para el Propietario.
--
-- Es IDEMPOTENTE: tabla e índice con IF NOT EXISTS, el permiso se inserta solo
-- si no existe, y el grant a Propietario/Auditor usa NOT EXISTS por
-- (role_id, permission_id).

CREATE TABLE IF NOT EXISTS {SCHEMA_NAME}.audit_logs (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    action VARCHAR(200) NOT NULL,
    entity_id UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_audit_logs_created_user ON {SCHEMA_NAME}.audit_logs (created_at, user_id);

INSERT INTO {SCHEMA_NAME}.permissions (id, module, action)
SELECT gen_random_uuid(), 'audit', 'read'
WHERE NOT EXISTS (
    SELECT 1 FROM {SCHEMA_NAME}.permissions WHERE module = 'audit' AND action = 'read'
);

-- Solo Propietario y Auditor leen el log (Matriz de Roles y Permisos v2): el
-- Administrador NO tiene acceso al log de auditoría según el documento.
INSERT INTO {SCHEMA_NAME}.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM {SCHEMA_NAME}.roles r
CROSS JOIN {SCHEMA_NAME}.permissions p
WHERE p.module = 'audit' AND p.action = 'read'
  AND (r.name = 'Propietario' OR r.name = 'Auditor')
  AND NOT EXISTS (
      SELECT 1 FROM {SCHEMA_NAME}.role_permissions rp
      WHERE rp.role_id = r.id AND rp.permission_id = p.id
  );
