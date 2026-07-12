WITH inserted_roles AS (
    INSERT INTO {SCHEMA_NAME}.roles (id, name, is_system, description)
    VALUES
        (gen_random_uuid(), 'Propietario', true, 'Acceso total al sistema'),
        (gen_random_uuid(), 'Administrador', true, 'Gestion operativa sin nomina ni usuarios'),
        (gen_random_uuid(), 'Cajero', true, 'Operacion de caja y ventas'),
        (gen_random_uuid(), 'Bodeguero', true, 'Gestion de inventario y proveedores'),
        (gen_random_uuid(), 'Auditor', true, 'Acceso de solo lectura para auditoria')
    RETURNING id, name
),
inserted_permissions AS (
    INSERT INTO {SCHEMA_NAME}.permissions (id, module, action)
    VALUES
        (gen_random_uuid(), 'inventory', 'read'),
        (gen_random_uuid(), 'inventory', 'write'),
        (gen_random_uuid(), 'inventory', 'delete'),
        (gen_random_uuid(), 'sales', 'read'),
        (gen_random_uuid(), 'sales', 'write'),
        (gen_random_uuid(), 'cash_register', 'read'),
        (gen_random_uuid(), 'cash_register', 'write'),
        (gen_random_uuid(), 'suppliers', 'read'),
        (gen_random_uuid(), 'suppliers', 'write'),
        (gen_random_uuid(), 'customers', 'read'),
        (gen_random_uuid(), 'customers', 'write'),
        (gen_random_uuid(), 'reports', 'read'),
        (gen_random_uuid(), 'reports', 'export'),
        (gen_random_uuid(), 'payroll', 'read'),
        (gen_random_uuid(), 'payroll', 'write'),
        (gen_random_uuid(), 'users', 'read'),
        (gen_random_uuid(), 'users', 'write')
    RETURNING id, module, action
)
INSERT INTO {SCHEMA_NAME}.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM inserted_roles r
CROSS JOIN inserted_permissions p
WHERE
    r.name = 'Propietario'
    OR (r.name = 'Administrador' AND p.module IN ('inventory', 'sales', 'cash_register', 'suppliers', 'customers', 'reports'))
    OR (r.name = 'Cajero' AND (
        (p.module = 'sales' AND p.action IN ('read', 'write'))
        OR (p.module = 'cash_register' AND p.action IN ('read', 'write'))
        OR (p.module = 'inventory' AND p.action = 'read')
        OR (p.module = 'customers' AND p.action = 'read')
    ))
    OR (r.name = 'Bodeguero' AND (
        (p.module = 'inventory' AND p.action IN ('read', 'write'))
        OR (p.module = 'suppliers' AND p.action = 'read')
    ))
    OR (r.name = 'Auditor' AND (
        (p.module IN ('inventory', 'sales', 'cash_register') AND p.action = 'read')
        OR (p.module = 'reports' AND p.action IN ('read', 'export'))
    ));
