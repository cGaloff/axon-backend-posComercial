CREATE TABLE {SCHEMA_NAME}.roles (
    id UUID PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    is_system BOOLEAN NOT NULL DEFAULT false,
    description TEXT,
    -- NULL = sin tope (Propietario/Administrador). Con valor = % máximo de
    -- descuento aplicable por un usuario con este rol sin autorización de un
    -- rol con tope superior (Matriz de Roles y Permisos v2, regla transversal C).
    max_discount_percentage DECIMAL(5, 2)
);

CREATE TABLE {SCHEMA_NAME}.permissions (
    id UUID PRIMARY KEY,
    module VARCHAR(100) NOT NULL,
    action VARCHAR(100) NOT NULL,
    UNIQUE (module, action)
);

CREATE TABLE {SCHEMA_NAME}.role_permissions (
    role_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.roles(id),
    permission_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.permissions(id),
    PRIMARY KEY (role_id, permission_id)
);

CREATE TABLE {SCHEMA_NAME}.users (
    id UUID PRIMARY KEY,
    full_name VARCHAR(200) NOT NULL,
    email VARCHAR(200) NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    -- PIN corto opcional (hash independiente de password_hash) para autorizar
    -- acciones de rol superior sin cerrar la sesión de quien las pide (Matriz de
    -- Roles y Permisos v2, rol Cajero).
    pin_hash VARCHAR(500),
    role_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.roles(id),
    is_active BOOLEAN NOT NULL DEFAULT true,
    -- Bloqueo por intentos fallidos + cierre de sesión por inactividad (Matriz
    -- de Roles y Permisos v2, "seguridad técnica").
    failed_login_attempts INT NOT NULL DEFAULT 0,
    locked_until TIMESTAMPTZ,
    last_activity_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE {SCHEMA_NAME}.units (
    id UUID PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    abbreviation VARCHAR(20) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true
);

CREATE TABLE {SCHEMA_NAME}.categories (
    id UUID PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    is_active BOOLEAN NOT NULL DEFAULT true
);

CREATE TABLE {SCHEMA_NAME}.warehouses (
    id UUID PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    is_default BOOLEAN NOT NULL DEFAULT false,
    is_active BOOLEAN NOT NULL DEFAULT true
);

CREATE TABLE {SCHEMA_NAME}.cash_registers (
    id UUID PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    is_default BOOLEAN NOT NULL DEFAULT false,
    is_active BOOLEAN NOT NULL DEFAULT true
);

-- Catálogo fijo de 8 impuestos colombianos (ver TaxCode en el backend): el
-- tenant no puede crear/editar impuestos personalizados, solo desactivar los
-- que no le apliquen a su negocio (is_active).
CREATE TABLE {SCHEMA_NAME}.tax_types (
    id UUID PRIMARY KEY,
    code VARCHAR(20) NOT NULL,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(300) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true
);

CREATE TABLE {SCHEMA_NAME}.products (
    id UUID PRIMARY KEY,
    sku VARCHAR(100) NOT NULL,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    price DECIMAL(12, 2) NOT NULL,
    cost DECIMAL(12, 2) NOT NULL,
    stock INT NOT NULL DEFAULT 0,
    min_stock INT NOT NULL DEFAULT 0,
    category_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.categories(id),
    unit_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.units(id),
    attributes JSONB,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Unicidad de SKU solo entre productos ACTIVOS: al desactivar (soft-delete)
-- un producto, su SKU queda libre para reutilizarse en uno nuevo.
CREATE UNIQUE INDEX idx_products_sku_active ON {SCHEMA_NAME}.products (sku) WHERE is_active;

CREATE INDEX idx_products_attributes ON {SCHEMA_NAME}.products USING GIN (attributes);

-- Impuestos vigentes configurados sobre cada producto (0 a N por producto,
-- porcentaje libre definido por el usuario, sin whitelist). Ver sale_item_taxes
-- para el snapshot histórico usado en las ventas ya realizadas.
CREATE TABLE {SCHEMA_NAME}.product_taxes (
    id UUID PRIMARY KEY,
    product_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.products(id) ON DELETE CASCADE,
    tax_type_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.tax_types(id),
    percentage NUMERIC(9, 4) NOT NULL,
    UNIQUE (product_id, tax_type_id)
);

CREATE TABLE {SCHEMA_NAME}.attribute_definitions (
    id UUID PRIMARY KEY,
    key VARCHAR(100) NOT NULL,
    label VARCHAR(200) NOT NULL,
    type VARCHAR(50) NOT NULL,
    options JSONB,
    category_id UUID REFERENCES {SCHEMA_NAME}.categories(id),
    is_filterable BOOLEAN NOT NULL DEFAULT false,
    sort_order INT NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT true,
    UNIQUE (key, category_id)
);

CREATE TABLE {SCHEMA_NAME}.inventory_movements (
    id UUID PRIMARY KEY,
    product_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.products(id),
    warehouse_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.warehouses(id),
    type VARCHAR(50) NOT NULL,
    quantity INT NOT NULL,
    stock_before INT NOT NULL,
    stock_after INT NOT NULL,
    reason VARCHAR(500),
    created_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    -- Applied/PendingApproval/Approved/Rejected: solo mermas (Loss) por encima de
    -- tenant_config.merma_approval_threshold quedan en PendingApproval (Matriz de
    -- Roles y Permisos v2, regla transversal B).
    status VARCHAR(30) NOT NULL DEFAULT 'Applied',
    reviewed_by UUID,
    reviewed_at TIMESTAMPTZ,
    rejection_reason VARCHAR(500)
);

CREATE INDEX idx_inventory_movements_product_created ON {SCHEMA_NAME}.inventory_movements (product_id, created_at);

CREATE TABLE {SCHEMA_NAME}.stock_alerts (
    id UUID PRIMARY KEY,
    product_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.products(id),
    warehouse_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.warehouses(id),
    current_stock INT NOT NULL,
    min_stock INT NOT NULL,
    is_read BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE {SCHEMA_NAME}.sales (
    id UUID PRIMARY KEY,
    sale_number VARCHAR(50) NOT NULL UNIQUE,
    customer_id UUID NULL,
    customer_name VARCHAR(200),
    customer_document_type VARCHAR(10),
    customer_document_number VARCHAR(30),
    status VARCHAR(50) NOT NULL DEFAULT 'Completed',
    total NUMERIC(12, 2) NOT NULL DEFAULT 0,
    -- Monto ya convertido (si se ingresó como %) del descuento aplicado a TODA
    -- la venta; repartido entre sale_items.general_discount_share. Solo para
    -- mostrarlo como línea propia en la factura (ver PdfService.ComposeTotals).
    general_discount_amount NUMERIC(12, 2) NOT NULL DEFAULT 0,
    -- Solo si el descuento general se ingresó como % (null si fue monto fijo):
    -- se guarda para mostrarlo junto al monto en la factura ("Descuento
    -- general (10%)"), no participa en ningún cálculo.
    general_discount_percentage NUMERIC(9, 4),
    notes TEXT,
    cash_register_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.cash_registers(id),
    created_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    voided_at TIMESTAMPTZ,
    voided_by UUID,
    void_reason TEXT,
    returned_at TIMESTAMPTZ,
    returned_by UUID,
    -- Quién PIDIÓ la anulación/devolución es voided_by/returned_by (p. ej. un
    -- Cajero); authorized_by es el Administrador/Propietario cuyo PIN la habilitó,
    -- NULL si quien la pidió ya tenía el permiso directamente (Matriz de Roles y
    -- Permisos v2, rol Cajero).
    authorized_by UUID
);

CREATE INDEX idx_sales_created_status ON {SCHEMA_NAME}.sales (created_at, status);

CREATE INDEX idx_sales_customer_id ON {SCHEMA_NAME}.sales (customer_id);

-- Pagos divididos: 1 a N formas de pago que cubren el total de la venta.
-- amount_tendered/change solo aplican a pagos en efectivo.
CREATE TABLE {SCHEMA_NAME}.sale_payments (
    id UUID PRIMARY KEY,
    sale_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.sales(id) ON DELETE CASCADE,
    method VARCHAR(50) NOT NULL,
    amount NUMERIC(12, 2) NOT NULL,
    amount_tendered NUMERIC(12, 2),
    change NUMERIC(12, 2)
);

CREATE INDEX idx_sale_payments_sale_id ON {SCHEMA_NAME}.sale_payments (sale_id);

CREATE TABLE {SCHEMA_NAME}.sale_items (
    id UUID PRIMARY KEY,
    sale_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.sales(id),
    product_id UUID NOT NULL,
    product_name VARCHAR(200) NOT NULL,
    product_sku VARCHAR(100) NOT NULL,
    unit_price NUMERIC(12, 2) NOT NULL,
    quantity INT NOT NULL,
    discount NUMERIC(12, 2) NOT NULL DEFAULT 0,
    -- Solo si el descuento de este producto se ingresó como % (null si fue
    -- monto fijo): se guarda para mostrarlo junto al monto en la factura
    -- ("Desc: (5%)"), no participa en ningún cálculo.
    discount_percentage NUMERIC(9, 4),
    general_discount_share NUMERIC(12, 2) NOT NULL DEFAULT 0,
    -- Costo unitario del producto AL MOMENTO DE LA VENTA (snapshot de
    -- products.cost, igual que unit_price) — no el costo actual. 0 = sin dato
    -- de costo (ventas anteriores a este campo, o producto sin costo cargado).
    unit_cost NUMERIC(12, 2) NOT NULL DEFAULT 0,
    subtotal NUMERIC(12, 2) NOT NULL,
    subtotal_base NUMERIC(12, 2) NOT NULL DEFAULT 0
);

-- Snapshot histórico de los impuestos aplicados a cada línea de venta (0 a N).
-- Sin FK a tax_types a propósito: el catálogo puede cambiar después y esta
-- fila debe seguir siendo válida tal cual quedó al momento de la venta.
CREATE TABLE {SCHEMA_NAME}.sale_item_taxes (
    id UUID PRIMARY KEY,
    sale_item_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.sale_items(id) ON DELETE CASCADE,
    tax_type_id UUID NOT NULL,
    tax_type_name VARCHAR(100) NOT NULL,
    percentage NUMERIC(9, 4) NOT NULL,
    amount NUMERIC(12, 2) NOT NULL
);

CREATE TABLE {SCHEMA_NAME}.sale_returns (
    id UUID PRIMARY KEY,
    sale_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.sales(id),
    reason TEXT NOT NULL,
    returned_by UUID NOT NULL,
    returned_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    total NUMERIC(12, 2) NOT NULL
);

-- Consecutivo de factura por tenant: cada tenant tiene su PROPIA secuencia en
-- su propio schema, por lo que nextval() nunca puede colisionar entre tenants
-- (son objetos de Postgres completamente independientes), y es atómica frente
-- a transacciones concurrentes dentro del mismo tenant.
CREATE SEQUENCE {SCHEMA_NAME}.invoice_number_seq START 1;

-- Registro auditable interno (no numeración legal tipo DIAN), congelado tal
-- como se generó: items/impuestos/pagos son un snapshot, no se recalculan.
CREATE TABLE {SCHEMA_NAME}.invoices (
    id UUID PRIMARY KEY,
    sale_id UUID NOT NULL UNIQUE REFERENCES {SCHEMA_NAME}.sales(id),
    number BIGINT NOT NULL UNIQUE,
    issued_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    sale_number VARCHAR(50) NOT NULL,
    customer_name VARCHAR(200),
    customer_document_number VARCHAR(30),
    total NUMERIC(12, 2) NOT NULL,
    general_discount_amount NUMERIC(12, 2) NOT NULL DEFAULT 0,
    general_discount_percentage NUMERIC(9, 4)
);

CREATE INDEX idx_invoices_issued_at ON {SCHEMA_NAME}.invoices (issued_at);

CREATE TABLE {SCHEMA_NAME}.invoice_payments (
    id UUID PRIMARY KEY,
    invoice_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.invoices(id) ON DELETE CASCADE,
    method VARCHAR(50) NOT NULL,
    amount NUMERIC(12, 2) NOT NULL,
    amount_tendered NUMERIC(12, 2),
    change NUMERIC(12, 2)
);

CREATE TABLE {SCHEMA_NAME}.invoice_items (
    id UUID PRIMARY KEY,
    invoice_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.invoices(id) ON DELETE CASCADE,
    product_id UUID NOT NULL,
    product_name VARCHAR(200) NOT NULL,
    product_sku VARCHAR(100) NOT NULL,
    unit_price NUMERIC(12, 2) NOT NULL,
    quantity INT NOT NULL,
    discount NUMERIC(12, 2) NOT NULL DEFAULT 0,
    discount_percentage NUMERIC(9, 4),
    general_discount_share NUMERIC(12, 2) NOT NULL DEFAULT 0,
    unit_cost NUMERIC(12, 2) NOT NULL DEFAULT 0,
    subtotal NUMERIC(12, 2) NOT NULL,
    subtotal_base NUMERIC(12, 2) NOT NULL DEFAULT 0
);

CREATE TABLE {SCHEMA_NAME}.invoice_item_taxes (
    id UUID PRIMARY KEY,
    invoice_item_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.invoice_items(id) ON DELETE CASCADE,
    tax_type_id UUID NOT NULL,
    tax_type_name VARCHAR(100) NOT NULL,
    percentage NUMERIC(9, 4) NOT NULL,
    amount NUMERIC(12, 2) NOT NULL
);

CREATE TABLE {SCHEMA_NAME}.cash_sessions (
    id UUID PRIMARY KEY,
    cash_register_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.cash_registers(id),
    opened_by UUID NOT NULL,
    closed_by UUID,
    opened_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    closed_at TIMESTAMPTZ,
    initial_amount NUMERIC(12, 2) NOT NULL DEFAULT 0,
    expected_amount NUMERIC(12, 2) NOT NULL DEFAULT 0,
    counted_amount NUMERIC(12, 2),
    difference NUMERIC(12, 2),
    status VARCHAR(50) NOT NULL DEFAULT 'Open',
    notes TEXT
);

CREATE INDEX idx_cash_sessions_register_status ON {SCHEMA_NAME}.cash_sessions (cash_register_id, status);

CREATE INDEX idx_cash_sessions_opened_by ON {SCHEMA_NAME}.cash_sessions (opened_by);

CREATE TABLE {SCHEMA_NAME}.cash_movements (
    id UUID PRIMARY KEY,
    cash_session_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.cash_sessions(id),
    type VARCHAR(50) NOT NULL,
    amount NUMERIC(12, 2) NOT NULL,
    description VARCHAR(500) NOT NULL,
    reference_id UUID,
    created_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_cash_movements_session_created ON {SCHEMA_NAME}.cash_movements (cash_session_id, created_at);

CREATE TABLE {SCHEMA_NAME}.tenant_config (
    id UUID PRIMARY KEY,
    business_name VARCHAR(200) NOT NULL,
    nit VARCHAR(20),
    address VARCHAR(500),
    phone VARCHAR(50),
    email VARCHAR(200),
    website VARCHAR(200),
    logo_url VARCHAR(500),
    is_responsable_iva BOOLEAN NOT NULL DEFAULT false,
    -- Umbral (en pesos) desde el cual una merma queda pendiente de aprobación de
    -- un Administrador. Default seguro en 0: hasta que se configure explícitamente,
    -- TODA merma requiere aprobación (Matriz de Roles y Permisos v2, regla B).
    merma_approval_threshold DECIMAL(12, 2) NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE {SCHEMA_NAME}.suppliers (
    id UUID PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    document_type VARCHAR(20) NOT NULL,
    document_number VARCHAR(20) NOT NULL,
    contact_name VARCHAR(200) NOT NULL,
    phone VARCHAR(50) NOT NULL,
    email VARCHAR(200) NOT NULL,
    address VARCHAR(500),
    city VARCHAR(100),
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (document_type, document_number)
);

CREATE TABLE {SCHEMA_NAME}.purchase_orders (
    id UUID PRIMARY KEY,
    supplier_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.suppliers(id),
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    notes TEXT,
    order_date TIMESTAMPTZ NOT NULL DEFAULT now(),
    expected_date TIMESTAMPTZ,
    -- Factura del proveedor para esta compra (referencia externa, no el id
    -- interno de purchase_orders) y el tipo de documento del proveedor
    -- snapshoteado al momento de la compra (no cambia si el proveedor edita su
    -- tipo de documento después).
    supplier_invoice_number VARCHAR(100),
    supplier_invoice_date TIMESTAMPTZ,
    supplier_document_type_at_purchase VARCHAR(20) NOT NULL,
    created_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_purchase_orders_supplier_status ON {SCHEMA_NAME}.purchase_orders (supplier_id, status);

CREATE INDEX idx_purchase_orders_created_by ON {SCHEMA_NAME}.purchase_orders (created_by);

CREATE TABLE {SCHEMA_NAME}.purchase_order_items (
    id UUID PRIMARY KEY,
    purchase_order_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.purchase_orders(id),
    product_id UUID NOT NULL,
    product_name VARCHAR(200) NOT NULL,
    product_sku VARCHAR(100) NOT NULL,
    quantity_ordered INT NOT NULL,
    quantity_received INT NOT NULL DEFAULT 0,
    unit_cost NUMERIC(12, 2) NOT NULL,
    subtotal NUMERIC(12, 2) NOT NULL,
    tax_amount NUMERIC(12, 2) NOT NULL DEFAULT 0
);

-- Snapshot de impuestos aplicados a cada línea de compra, tomados de
-- ProductTax vigente al momento de crear la orden (mismo patrón que
-- sale_item_taxes). Sin FK a tax_types a propósito: histórico, no vigente.
CREATE TABLE {SCHEMA_NAME}.purchase_order_item_taxes (
    id UUID PRIMARY KEY,
    purchase_order_item_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.purchase_order_items(id) ON DELETE CASCADE,
    tax_type_id UUID NOT NULL,
    tax_type_name VARCHAR(100) NOT NULL,
    percentage NUMERIC(9, 4) NOT NULL,
    amount NUMERIC(12, 2) NOT NULL
);

CREATE TABLE {SCHEMA_NAME}.purchase_receipts (
    id UUID PRIMARY KEY,
    purchase_order_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.purchase_orders(id),
    received_by UUID NOT NULL,
    received_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    notes TEXT,
    total_received NUMERIC(12, 2) NOT NULL DEFAULT 0
);

CREATE INDEX idx_purchase_receipts_order_received ON {SCHEMA_NAME}.purchase_receipts (purchase_order_id, received_at);

CREATE TABLE {SCHEMA_NAME}.purchase_receipt_items (
    id UUID PRIMARY KEY,
    purchase_receipt_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.purchase_receipts(id),
    purchase_order_item_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.purchase_order_items(id),
    product_id UUID NOT NULL,
    product_name VARCHAR(200) NOT NULL,
    quantity_received INT NOT NULL,
    unit_cost NUMERIC(12, 2) NOT NULL,
    subtotal NUMERIC(12, 2) NOT NULL
);

CREATE TABLE {SCHEMA_NAME}.supplier_payments (
    id UUID PRIMARY KEY,
    supplier_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.suppliers(id),
    amount NUMERIC(12, 2) NOT NULL,
    paid_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    payment_method VARCHAR(50) NOT NULL,
    reference VARCHAR(200),
    notes TEXT,
    created_by UUID NOT NULL
);

CREATE INDEX idx_supplier_payments_supplier_paid ON {SCHEMA_NAME}.supplier_payments (supplier_id, paid_at);

CREATE TABLE {SCHEMA_NAME}.product_suppliers (
    product_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.products(id),
    supplier_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.suppliers(id),
    last_purchase_price NUMERIC(12, 2) NOT NULL,
    is_preferred BOOLEAN NOT NULL DEFAULT false,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (product_id, supplier_id)
);

CREATE TABLE {SCHEMA_NAME}.refresh_tokens (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.users(id) ON DELETE CASCADE,
    token_hash VARCHAR(64) NOT NULL UNIQUE,
    expires_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    revoked_at TIMESTAMPTZ
);

CREATE INDEX idx_refresh_tokens_user ON {SCHEMA_NAME}.refresh_tokens (user_id);

-- Registro de auditoría inmutable (Matriz de Roles y Permisos v2, regla A): no
-- hay UPDATE/DELETE en el código de la aplicación sobre esta tabla, ni
-- siquiera para el Propietario — solo INSERT desde AuditLoggingBehavior y
-- SELECT desde la consulta de auditoría.
CREATE TABLE {SCHEMA_NAME}.audit_logs (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    action VARCHAR(200) NOT NULL,
    entity_id UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_audit_logs_created_user ON {SCHEMA_NAME}.audit_logs (created_at, user_id);

-- Historial de intentos de login (Matriz de Roles y Permisos v2, "seguridad
-- técnica"). user_id es NULL si el email no correspondía a ningún usuario real.
CREATE TABLE {SCHEMA_NAME}.login_attempts (
    id UUID PRIMARY KEY,
    email VARCHAR(200) NOT NULL,
    user_id UUID,
    success BOOLEAN NOT NULL,
    ip_address VARCHAR(64),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_login_attempts_created_user ON {SCHEMA_NAME}.login_attempts (created_at, user_id);
