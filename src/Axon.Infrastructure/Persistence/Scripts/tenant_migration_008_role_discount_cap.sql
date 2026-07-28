-- Migración 008: tope de descuento configurable por rol (Matriz de Roles y
-- Permisos v2, regla transversal C). Cajero queda con un 10% de partida; el
-- resto de roles sin tope (NULL) — igual que en tenant_seed.sql para tenants
-- nuevos.
--
-- Es IDEMPOTENTE: la columna se agrega con IF NOT EXISTS, y el UPDATE de Cajero
-- solo toca filas donde el tope aún no se ha configurado (NULL).

ALTER TABLE {SCHEMA_NAME}.roles
    ADD COLUMN IF NOT EXISTS max_discount_percentage DECIMAL(5, 2);

UPDATE {SCHEMA_NAME}.roles
SET max_discount_percentage = 10
WHERE name = 'Cajero' AND max_discount_percentage IS NULL;
