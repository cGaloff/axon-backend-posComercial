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
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
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
