CREATE TABLE {SCHEMA_NAME}.roles (
    id UUID PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    is_system BOOLEAN NOT NULL DEFAULT false,
    description TEXT
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
    role_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.roles(id),
    is_active BOOLEAN NOT NULL DEFAULT true,
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

CREATE TABLE {SCHEMA_NAME}.products (
    id UUID PRIMARY KEY,
    sku VARCHAR(100) NOT NULL UNIQUE,
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
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    tax_percentage NUMERIC(5, 2) NOT NULL DEFAULT 0
);

CREATE INDEX idx_products_attributes ON {SCHEMA_NAME}.products USING GIN (attributes);

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
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
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
    payment_method VARCHAR(50) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Completed',
    total NUMERIC(12, 2) NOT NULL DEFAULT 0,
    amount_paid NUMERIC(12, 2) NOT NULL DEFAULT 0,
    change NUMERIC(12, 2) NOT NULL DEFAULT 0,
    notes TEXT,
    cash_register_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.cash_registers(id),
    created_by UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    voided_at TIMESTAMPTZ,
    voided_by UUID,
    void_reason TEXT,
    returned_at TIMESTAMPTZ,
    returned_by UUID
);

CREATE INDEX idx_sales_created_status ON {SCHEMA_NAME}.sales (created_at, status);

CREATE INDEX idx_sales_customer_id ON {SCHEMA_NAME}.sales (customer_id);

CREATE TABLE {SCHEMA_NAME}.sale_items (
    id UUID PRIMARY KEY,
    sale_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.sales(id),
    product_id UUID NOT NULL,
    product_name VARCHAR(200) NOT NULL,
    product_sku VARCHAR(100) NOT NULL,
    unit_price NUMERIC(12, 2) NOT NULL,
    quantity INT NOT NULL,
    discount NUMERIC(12, 2) NOT NULL DEFAULT 0,
    subtotal NUMERIC(12, 2) NOT NULL,
    tax_percentage NUMERIC(5, 2) NOT NULL DEFAULT 0,
    tax_amount NUMERIC(12, 2) NOT NULL DEFAULT 0,
    subtotal_base NUMERIC(12, 2) NOT NULL DEFAULT 0
);

CREATE TABLE {SCHEMA_NAME}.sale_returns (
    id UUID PRIMARY KEY,
    sale_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.sales(id),
    reason TEXT NOT NULL,
    returned_by UUID NOT NULL,
    returned_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    total NUMERIC(12, 2) NOT NULL
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
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
