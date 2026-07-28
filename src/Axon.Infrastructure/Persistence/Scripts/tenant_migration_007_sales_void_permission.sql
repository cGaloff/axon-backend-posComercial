-- Migración 007: permiso "sales:void" (anular/devolver ventas), Matriz de Roles
-- y Permisos v2 — el Cajero no puede anular ni devolver sus propias ventas sin
-- autorización de un Administrador; hoy ambas acciones vivían bajo "sales:write",
-- que el Cajero sí tiene, así que quedaban abiertas por error.
--
-- Es IDEMPOTENTE: el permiso se inserta solo si no existe ya, y el grant a
-- Administrador/Propietario usa NOT EXISTS por (role_id, permission_id).

INSERT INTO {SCHEMA_NAME}.permissions (id, module, action)
SELECT gen_random_uuid(), 'sales', 'void'
WHERE NOT EXISTS (
    SELECT 1 FROM {SCHEMA_NAME}.permissions WHERE module = 'sales' AND action = 'void'
);

-- Propietario (todos los permisos) y Administrador (todo el módulo 'sales')
-- reciben el permiso nuevo; Cajero, Bodeguero y Auditor quedan excluidos a
-- propósito, igual que en tenant_seed.sql.
INSERT INTO {SCHEMA_NAME}.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM {SCHEMA_NAME}.roles r
CROSS JOIN {SCHEMA_NAME}.permissions p
WHERE p.module = 'sales' AND p.action = 'void'
  AND (r.name = 'Propietario' OR r.name = 'Administrador')
  AND NOT EXISTS (
      SELECT 1 FROM {SCHEMA_NAME}.role_permissions rp
      WHERE rp.role_id = r.id AND rp.permission_id = p.id
  );
