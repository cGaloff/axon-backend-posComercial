-- Migración 006: permitir reutilizar el SKU de un producto desactivado.
--
-- Bug reportado por frontend: al desactivar (soft-delete, is_active = false)
-- un producto, su SKU quedaba bloqueado para siempre porque la restricción
-- UNIQUE de la columna "sku" era incondicional (aplicaba también a productos
-- inactivos). Se reemplaza por un índice único PARCIAL que solo considera
-- productos activos, igual que ya hace tenant_schema_template.sql para
-- tenants nuevos desde este cambio.
--
-- Es IDEMPOTENTE: el nombre de la restricción UNIQUE de columna generado por
-- Postgres para "sku VARCHAR(100) NOT NULL UNIQUE" es products_sku_key; se
-- elimina con IF EXISTS y el índice nuevo se crea con IF NOT EXISTS, así que
-- correr este script más de una vez no falla.

ALTER TABLE {SCHEMA_NAME}.products DROP CONSTRAINT IF EXISTS products_sku_key;

CREATE UNIQUE INDEX IF NOT EXISTS idx_products_sku_active
    ON {SCHEMA_NAME}.products (sku) WHERE is_active;
