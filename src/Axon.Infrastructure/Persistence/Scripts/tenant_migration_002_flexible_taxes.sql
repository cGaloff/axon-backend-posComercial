-- Migración 002: modelo de impuestos flexible (TaxType / ProductTax / SaleItemTax).
--
-- Propósito: propagar a un tenant YA APROVISIONADO el mismo esquema que
-- tenant_schema_template.sql ya crea para tenants nuevos. No hay un sistema
-- formal de versión de schema por tenant en este proyecto (deuda técnica
-- documentada aparte), así que este script se aplica manualmente, una vez por
-- tenant, vía scripts/migrate-tax-model.ps1.
--
-- Es IDEMPOTENTE: correrlo dos veces sobre el mismo schema no falla ni
-- duplica datos (las tablas usan IF NOT EXISTS, y el backfill de datos
-- históricos está guardado por la existencia de las columnas viejas, que se
-- eliminan al final de cada bloque).

CREATE TABLE IF NOT EXISTS {SCHEMA_NAME}.tax_types (
    id UUID PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    code VARCHAR(20),
    is_active BOOLEAN NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS {SCHEMA_NAME}.product_taxes (
    id UUID PRIMARY KEY,
    product_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.products(id) ON DELETE CASCADE,
    tax_type_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.tax_types(id),
    percentage NUMERIC(9, 4) NOT NULL,
    UNIQUE (product_id, tax_type_id)
);

CREATE TABLE IF NOT EXISTS {SCHEMA_NAME}.sale_item_taxes (
    id UUID PRIMARY KEY,
    sale_item_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.sale_items(id) ON DELETE CASCADE,
    tax_type_id UUID NOT NULL,
    tax_type_name VARCHAR(100) NOT NULL,
    percentage NUMERIC(9, 4) NOT NULL,
    amount NUMERIC(12, 2) NOT NULL
);

-- El modelo anterior solo soportaba IVA; se crea ese TaxType si el tenant
-- todavía no tiene ninguno con ese código (por si el script ya corrió antes).
INSERT INTO {SCHEMA_NAME}.tax_types (id, name, code, is_active)
SELECT gen_random_uuid(), 'IVA', 'IVA', true
WHERE NOT EXISTS (
    SELECT 1 FROM {SCHEMA_NAME}.tax_types WHERE code = 'IVA'
);

-- Backfill de productos: cada producto con el viejo tax_percentage > 0 recibe
-- un ProductTax de IVA equivalente. Guardado por la existencia de la columna
-- vieja para poder correr este script más de una vez sin error.
DO $do$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = '{SCHEMA_NAME}' AND table_name = 'products' AND column_name = 'tax_percentage'
    ) THEN
        EXECUTE '
            INSERT INTO {SCHEMA_NAME}.product_taxes (id, product_id, tax_type_id, percentage)
            SELECT gen_random_uuid(), p.id, t.id, p.tax_percentage
            FROM {SCHEMA_NAME}.products p
            CROSS JOIN (SELECT id FROM {SCHEMA_NAME}.tax_types WHERE code = ''IVA'' LIMIT 1) t
            WHERE p.tax_percentage > 0
        ';

        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.products DROP COLUMN tax_percentage';
    END IF;
END
$do$;

-- Backfill de ventas históricas: cada línea de venta con el viejo
-- tax_percentage > 0 recibe su snapshot de impuesto "IVA" equivalente,
-- preservando el porcentaje y el monto ya calculados en su momento.
DO $do$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = '{SCHEMA_NAME}' AND table_name = 'sale_items' AND column_name = 'tax_percentage'
    ) THEN
        EXECUTE '
            INSERT INTO {SCHEMA_NAME}.sale_item_taxes (id, sale_item_id, tax_type_id, tax_type_name, percentage, amount)
            SELECT gen_random_uuid(), si.id, t.id, ''IVA'', si.tax_percentage, si.tax_amount
            FROM {SCHEMA_NAME}.sale_items si
            CROSS JOIN (SELECT id FROM {SCHEMA_NAME}.tax_types WHERE code = ''IVA'' LIMIT 1) t
            WHERE si.tax_percentage > 0
        ';

        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.sale_items DROP COLUMN tax_percentage';
        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.sale_items DROP COLUMN tax_amount';
    END IF;
END
$do$;
