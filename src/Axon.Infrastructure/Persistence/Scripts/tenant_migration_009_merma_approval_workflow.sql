-- Migración 009: flujo de aprobación de mermas (Matriz de Roles y Permisos v2,
-- regla transversal B). Una merma (Loss) que supere tenant_config.merma_approval_threshold
-- queda pendiente de aprobación de un Administrador en vez de aplicarse de inmediato.
--
-- Es IDEMPOTENTE: columnas con IF NOT EXISTS, el permiso se inserta solo si no
-- existe, y el grant a Propietario/Administrador usa NOT EXISTS por
-- (role_id, permission_id).

ALTER TABLE {SCHEMA_NAME}.tenant_config
    ADD COLUMN IF NOT EXISTS merma_approval_threshold DECIMAL(12, 2) NOT NULL DEFAULT 0;

ALTER TABLE {SCHEMA_NAME}.inventory_movements
    ADD COLUMN IF NOT EXISTS status VARCHAR(30) NOT NULL DEFAULT 'Applied',
    ADD COLUMN IF NOT EXISTS reviewed_by UUID,
    ADD COLUMN IF NOT EXISTS reviewed_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS rejection_reason VARCHAR(500);

INSERT INTO {SCHEMA_NAME}.permissions (id, module, action)
SELECT gen_random_uuid(), 'inventory', 'approve_movements'
WHERE NOT EXISTS (
    SELECT 1 FROM {SCHEMA_NAME}.permissions WHERE module = 'inventory' AND action = 'approve_movements'
);

-- Propietario y Administrador reciben el permiso nuevo; Bodeguero queda
-- excluido a propósito (no puede aprobar sus propias mermas).
INSERT INTO {SCHEMA_NAME}.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM {SCHEMA_NAME}.roles r
CROSS JOIN {SCHEMA_NAME}.permissions p
WHERE p.module = 'inventory' AND p.action = 'approve_movements'
  AND (r.name = 'Propietario' OR r.name = 'Administrador')
  AND NOT EXISTS (
      SELECT 1 FROM {SCHEMA_NAME}.role_permissions rp
      WHERE rp.role_id = r.id AND rp.permission_id = p.id
  );
