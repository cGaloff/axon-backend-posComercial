-- Migración 005: proveedores (tipo de documento, campos obligatorios) y
-- compras (factura del proveedor, tipo de proveedor, impuestos por línea).
--
-- Se aplica reutilizando el runner genérico scripts/migrate-tax-model.ps1
-- -MigrationSqlPath ...tenant_migration_005_suppliers_and_purchases.sql (ver
-- prompt 3 para el porqué de este mecanismo). Es IDEMPOTENTE.
--
-- ADVERTENCIA DE CALIDAD DE DATOS: contact_name/phone/email/document_number
-- eran opcionales antes de esta migración. Los proveedores existentes que no
-- tenían alguno de estos datos reciben un valor placeholder para poder aplicar
-- la restricción NOT NULL — quedan marcados con ese placeholder y deben
-- corregirse manualmente desde el módulo de proveedores después de migrar.
--
-- LIMITACIÓN RECONOCIDA: las compras históricas no registraron el tipo de
-- documento del proveedor al momento de la compra (columna nueva), así que se
-- usa el tipo de documento ACTUAL del proveedor como mejor aproximación
-- disponible — puede no coincidir con lo que era en el momento real de la compra.

-- 1) Proveedores: tipo/número de documento (reemplaza nit), campos obligatorios.
DO $do$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = '{SCHEMA_NAME}' AND table_name = 'suppliers' AND column_name = 'nit'
    ) THEN
        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.suppliers ADD COLUMN document_type VARCHAR(20)';
        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.suppliers ADD COLUMN document_number VARCHAR(20)';

        EXECUTE '
            UPDATE {SCHEMA_NAME}.suppliers
            SET document_type = ''NIT'',
                document_number = COALESCE(NULLIF(nit, ''''), ''sin-documento-'' || id::text)
        ';

        EXECUTE '
            UPDATE {SCHEMA_NAME}.suppliers
            SET contact_name = COALESCE(NULLIF(contact_name, ''''), ''Sin definir''),
                phone = COALESCE(NULLIF(phone, ''''), ''Sin definir''),
                email = COALESCE(NULLIF(email, ''''), ''sin-definir@sin-definir.com'')
            WHERE contact_name IS NULL OR contact_name = ''''
               OR phone IS NULL OR phone = ''''
               OR email IS NULL OR email = ''''
        ';

        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.suppliers ALTER COLUMN document_type SET NOT NULL';
        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.suppliers ALTER COLUMN document_number SET NOT NULL';
        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.suppliers ALTER COLUMN contact_name SET NOT NULL';
        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.suppliers ALTER COLUMN phone SET NOT NULL';
        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.suppliers ALTER COLUMN email SET NOT NULL';

        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.suppliers DROP COLUMN nit';
        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.suppliers DROP COLUMN payment_term_days';

        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.suppliers ADD CONSTRAINT uq_suppliers_document UNIQUE (document_type, document_number)';
    END IF;
END
$do$;

-- 2) Compras: factura del proveedor + tipo de proveedor al momento de la compra.
DO $do$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = '{SCHEMA_NAME}' AND table_name = 'purchase_orders' AND column_name = 'supplier_invoice_number'
    ) THEN
        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.purchase_orders ADD COLUMN supplier_invoice_number VARCHAR(100)';
        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.purchase_orders ADD COLUMN supplier_invoice_date TIMESTAMPTZ';
        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.purchase_orders ADD COLUMN supplier_document_type_at_purchase VARCHAR(20)';

        EXECUTE '
            UPDATE {SCHEMA_NAME}.purchase_orders po
            SET supplier_document_type_at_purchase = s.document_type
            FROM {SCHEMA_NAME}.suppliers s
            WHERE s.id = po.supplier_id
        ';

        -- Fallback por si algún proveedor referenciado ya no existiera.
        EXECUTE '
            UPDATE {SCHEMA_NAME}.purchase_orders
            SET supplier_document_type_at_purchase = ''NIT''
            WHERE supplier_document_type_at_purchase IS NULL
        ';

        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.purchase_orders ALTER COLUMN supplier_document_type_at_purchase SET NOT NULL';
    END IF;
END
$do$;

-- 3) Impuestos por línea de compra (snapshot de ProductTax vigente al crear la orden).
ALTER TABLE {SCHEMA_NAME}.purchase_order_items ADD COLUMN IF NOT EXISTS tax_amount NUMERIC(12, 2) NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS {SCHEMA_NAME}.purchase_order_item_taxes (
    id UUID PRIMARY KEY,
    purchase_order_item_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.purchase_order_items(id) ON DELETE CASCADE,
    tax_type_id UUID NOT NULL,
    tax_type_name VARCHAR(100) NOT NULL,
    percentage NUMERIC(9, 4) NOT NULL,
    amount NUMERIC(12, 2) NOT NULL
);
