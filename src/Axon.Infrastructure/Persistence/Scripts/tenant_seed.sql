WITH inserted_roles AS (
    -- Cajero: tope de descuento 10% (Matriz de Roles y Permisos v2, regla
    -- transversal C) — el resto queda sin tope (NULL).
    INSERT INTO {SCHEMA_NAME}.roles (id, name, is_system, description, max_discount_percentage)
    VALUES
        (gen_random_uuid(), 'Propietario', true, 'Acceso total al sistema', NULL),
        (gen_random_uuid(), 'Administrador', true, 'Gestion operativa sin nomina ni usuarios', NULL),
        (gen_random_uuid(), 'Cajero', true, 'Operacion de caja y ventas', 10),
        (gen_random_uuid(), 'Bodeguero', true, 'Gestion de inventario y proveedores', NULL),
        (gen_random_uuid(), 'Auditor', true, 'Acceso de solo lectura para auditoria', NULL)
    RETURNING id, name
),
inserted_permissions AS (
    INSERT INTO {SCHEMA_NAME}.permissions (id, module, action)
    VALUES
        (gen_random_uuid(), 'inventory', 'read'),
        (gen_random_uuid(), 'inventory', 'write'),
        (gen_random_uuid(), 'inventory', 'delete'),
        (gen_random_uuid(), 'inventory', 'approve_movements'),
        (gen_random_uuid(), 'sales', 'read'),
        (gen_random_uuid(), 'sales', 'write'),
        (gen_random_uuid(), 'sales', 'void'),
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
        (gen_random_uuid(), 'configuration', 'write'),
        (gen_random_uuid(), 'audit', 'read'),
        (gen_random_uuid(), 'roles', 'write')
    RETURNING id, module, action
)
-- Nota sobre 'sales:void' (anular/devolver ventas, Matriz de Roles y Permisos
-- v2): el Administrador recibe TODAS las acciones del módulo 'sales' vía el
-- "p.module IN (...)" de abajo, así que automáticamente hereda 'void' sin
-- listarlo aparte. El Cajero en cambio solo lista 'read'/'write' explícitamente
-- más abajo, así que queda excluido de 'void' a propósito — no puede anular ni
-- devolver sus propias ventas sin autorización de un Administrador.
INSERT INTO {SCHEMA_NAME}.role_permissions (role_id, permission_id)
SELECT r.id, p.id
FROM inserted_roles r
CROSS JOIN inserted_permissions p
WHERE
    r.name = 'Propietario'
    -- 'roles:write' se lista aparte (no en el módulo wildcard) porque solo cubre
    -- el tope de descuento por rol (Matriz de Roles y Permisos v2, regla C) —
    -- no la gestión completa de roles/permisos, que sigue siendo del Propietario.
    OR (r.name = 'Administrador' AND (
        p.module IN ('inventory', 'sales', 'cash_register', 'suppliers', 'customers', 'reports')
        OR (p.module = 'configuration' AND p.action = 'read')
        OR (p.module = 'roles' AND p.action = 'write')
    ))
    OR (r.name = 'Cajero' AND (
        (p.module = 'sales' AND p.action IN ('read', 'write'))
        OR (p.module = 'cash_register' AND p.action IN ('read', 'write'))
        OR (p.module = 'inventory' AND p.action = 'read')
        OR (p.module = 'customers' AND p.action = 'read')
    ))
    -- Bodeguero solo lista 'read'/'write' explícitamente, así que queda excluido
    -- de 'inventory:approve_movements' a propósito: no puede aprobar sus propias
    -- mermas (Matriz de Roles y Permisos v2, rol Bodeguero).
    OR (r.name = 'Bodeguero' AND (
        (p.module = 'inventory' AND p.action IN ('read', 'write'))
        OR (p.module = 'suppliers' AND p.action = 'read')
    ))
    -- 'audit:read' solo para Propietario (arriba, todo el permiso) y Auditor
    -- (aquí) — el Administrador queda excluido a propósito, no está en su
    -- lista de módulos: el documento no le da acceso al log de auditoría.
    OR (r.name = 'Auditor' AND (
        (p.module IN ('inventory', 'sales', 'cash_register', 'suppliers') AND p.action = 'read')
        OR (p.module = 'reports' AND p.action IN ('read', 'export'))
        OR (p.module = 'configuration' AND p.action = 'read')
        OR (p.module = 'audit' AND p.action = 'read')
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

-- Catálogo fijo de impuestos colombianos (ver TaxCode en el backend): el
-- tenant no puede crear ni editar impuestos personalizados, solo desactivar
-- los que no le apliquen a su negocio.
INSERT INTO {SCHEMA_NAME}.tax_types (id, code, name, description, is_active)
VALUES
    (gen_random_uuid(), 'Iva', 'IVA', 'Impuesto sobre las ventas', true),
    (gen_random_uuid(), 'Gmf', 'GMF', 'Gravamen a los movimientos financieros', true),
    (gen_random_uuid(), 'Inc', 'INC', 'Impuesto nacional al consumo', true),
    (gen_random_uuid(), 'IncPl', 'INC PL', 'Impuesto nacional al consumo de bolsas plásticas', true),
    (gen_random_uuid(), 'Ica', 'ICA', 'Impuesto de timbre departamental', true),
    (gen_random_uuid(), 'Incombustible', 'Incombustible', 'Impuesto nacional a la gasolina y al ACPM', true),
    (gen_random_uuid(), 'Incarbono', 'Incarbono', 'Impuesto nacional al carbono', true),
    (gen_random_uuid(), 'Ibua', 'IBUA', 'Impuesto nacional a las bebidas azucaradas', true);
