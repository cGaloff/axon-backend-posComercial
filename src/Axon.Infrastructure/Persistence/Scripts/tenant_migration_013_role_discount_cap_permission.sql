-- Migración 013: permiso "roles:write" para editar el tope de descuento por rol
-- (Matriz de Roles y Permisos v2, regla C — "el Administrador define el tope de
-- descuento del Cajero"). Cubre SOLO el tope de descuento, no la gestión
-- completa de roles/permisos (que sigue siendo del Propietario).
--
-- Es IDEMPOTENTE: el permiso se inserta solo si no existe, y el grant a
-- Propietario/Administrador usa NOT EXISTS por (role_id, permission_id).

INSERT INTO {SCHEMA_NAME}.permissions (id, module, action)
SELECT gen_random_uuid(), 'roles', 'write'
WHERE NOT EXISTS (
    SELECT 1 FROM {SCHEMA_NAME}.permissions WHERE module = 'roles' AND action = 'write'
);

INSERT INTO {SCHEMA_NAME}.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM {SCHEMA_NAME}.roles r
CROSS JOIN {SCHEMA_NAME}.permissions p
WHERE p.module = 'roles' AND p.action = 'write'
  AND (r.name = 'Propietario' OR r.name = 'Administrador')
  AND NOT EXISTS (
      SELECT 1 FROM {SCHEMA_NAME}.role_permissions rp
      WHERE rp.role_id = r.id AND rp.permission_id = p.id
  );
