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
        (gen_random_uuid(), 'users', 'write'),
        (gen_random_uuid(), 'configuration', 'read'),
        (gen_random_uuid(), 'configuration', 'write')
    RETURNING id, module, action
)
INSERT INTO {SCHEMA_NAME}.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM inserted_roles r
CROSS JOIN inserted_permissions p
WHERE
    r.name = 'Propietario'
    OR (r.name = 'Administrador' AND (
        p.module IN ('inventory', 'sales', 'cash_register', 'suppliers', 'customers', 'reports')
        OR (p.module = 'configuration' AND p.action = 'read')
    ))
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
        OR (p.module = 'configuration' AND p.action = 'read')
    ));

INSERT INTO {SCHEMA_NAME}.units (id, name, abbreviation, is_active)
VALUES
    (gen_random_uuid(), 'Unidad', 'und', true),
    (gen_random_uuid(), 'Caja', 'cja', true),
    (gen_random_uuid(), 'Kilogramo', 'kg', true),
    (gen_random_uuid(), 'Metro', 'mt', true),
    (gen_random_uuid(), 'Litro', 'lt', true),
    (gen_random_uuid(), 'Par', 'par', true);

INSERT INTO {SCHEMA_NAME}.warehouses (id, name, description, is_default, is_active)
VALUES (gen_random_uuid(), 'Tienda Principal', 'Bodega principal', true, true);

INSERT INTO {SCHEMA_NAME}.cash_registers (id, name, description, is_default, is_active)
VALUES (gen_random_uuid(), 'Caja Principal', 'Caja principal de la tienda', true, true);

INSERT INTO {SCHEMA_NAME}.tenant_config (id, business_name, is_responsable_iva, created_at, updated_at)
VALUES (gen_random_uuid(), 'Mi Tienda', false, now(), now());
