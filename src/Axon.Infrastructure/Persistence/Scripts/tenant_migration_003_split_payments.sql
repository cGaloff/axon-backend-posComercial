-- Migración 003: pagos divididos (SalePayment).
--
-- Propósito: propagar a un tenant YA APROVISIONADO el mismo esquema que
-- tenant_schema_template.sql ya crea para tenants nuevos. Se aplica manualmente,
-- una vez por tenant, reutilizando el runner genérico scripts/migrate-tax-model.ps1
-- con -MigrationSqlPath apuntando a este archivo (no hay un sistema formal de
-- versión de schema por tenant en este proyecto — ver resumen del prompt 3).
--
-- Es IDEMPOTENTE: correrlo dos veces sobre el mismo schema no falla ni duplica
-- datos (tabla con IF NOT EXISTS, backfill guardado por existencia de las
-- columnas viejas que se eliminan al final).

CREATE TABLE IF NOT EXISTS {SCHEMA_NAME}.sale_payments (
    id UUID PRIMARY KEY,
    sale_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.sales(id) ON DELETE CASCADE,
    method VARCHAR(50) NOT NULL,
    amount NUMERIC(12, 2) NOT NULL,
    amount_tendered NUMERIC(12, 2),
    change NUMERIC(12, 2)
);

CREATE INDEX IF NOT EXISTS idx_sale_payments_sale_id ON {SCHEMA_NAME}.sale_payments (sale_id);

-- Backfill: cada venta existente tenía UN solo método de pago cubriendo el 100%
-- del total (sales.payment_method/amount_paid/change). Se convierte en su
-- SalePayment equivalente antes de eliminar esas columnas de sales.
DO $do$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = '{SCHEMA_NAME}' AND table_name = 'sales' AND column_name = 'payment_method'
    ) THEN
        EXECUTE '
            INSERT INTO {SCHEMA_NAME}.sale_payments (id, sale_id, method, amount, amount_tendered, change)
            SELECT
                gen_random_uuid(),
                s.id,
                s.payment_method,
                s.total,
                CASE WHEN s.payment_method = ''Cash'' THEN s.amount_paid ELSE NULL END,
                CASE WHEN s.payment_method = ''Cash'' THEN s.change ELSE NULL END
            FROM {SCHEMA_NAME}.sales s
        ';

        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.sales DROP COLUMN payment_method';
        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.sales DROP COLUMN amount_paid';
        EXECUTE 'ALTER TABLE {SCHEMA_NAME}.sales DROP COLUMN change';
    END IF;
END
$do$;
