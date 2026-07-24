-- Migración 004: factura como registro auditable (Invoice).
--
-- Propósito: propagar a un tenant YA APROVISIONADO el mismo esquema que
-- tenant_schema_template.sql ya crea para tenants nuevos. Se aplica reutilizando
-- el runner genérico scripts/migrate-tax-model.ps1 con -MigrationSqlPath
-- apuntando a este archivo (no hay un sistema formal de versión de schema por
-- tenant en este proyecto — ver resumen del prompt 3).
--
-- Es IDEMPOTENTE: correrlo dos veces sobre el mismo schema no falla ni duplica
-- datos. No hay backfill de ventas históricas: Invoice es un concepto nuevo, no
-- reemplaza ninguna columna existente, así que no hay nada que migrar desde
-- datos viejos (a diferencia de las migraciones 002 y 003).

CREATE SEQUENCE IF NOT EXISTS {SCHEMA_NAME}.invoice_number_seq START 1;

CREATE TABLE IF NOT EXISTS {SCHEMA_NAME}.invoices (
    id UUID PRIMARY KEY,
    sale_id UUID NOT NULL UNIQUE REFERENCES {SCHEMA_NAME}.sales(id),
    number BIGINT NOT NULL UNIQUE,
    issued_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    sale_number VARCHAR(50) NOT NULL,
    customer_name VARCHAR(200),
    total NUMERIC(12, 2) NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_invoices_issued_at ON {SCHEMA_NAME}.invoices (issued_at);

CREATE TABLE IF NOT EXISTS {SCHEMA_NAME}.invoice_payments (
    id UUID PRIMARY KEY,
    invoice_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.invoices(id) ON DELETE CASCADE,
    method VARCHAR(50) NOT NULL,
    amount NUMERIC(12, 2) NOT NULL,
    amount_tendered NUMERIC(12, 2),
    change NUMERIC(12, 2)
);

CREATE TABLE IF NOT EXISTS {SCHEMA_NAME}.invoice_items (
    id UUID PRIMARY KEY,
    invoice_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.invoices(id) ON DELETE CASCADE,
    product_id UUID NOT NULL,
    product_name VARCHAR(200) NOT NULL,
    product_sku VARCHAR(100) NOT NULL,
    unit_price NUMERIC(12, 2) NOT NULL,
    quantity INT NOT NULL,
    discount NUMERIC(12, 2) NOT NULL DEFAULT 0,
    subtotal NUMERIC(12, 2) NOT NULL,
    subtotal_base NUMERIC(12, 2) NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS {SCHEMA_NAME}.invoice_item_taxes (
    id UUID PRIMARY KEY,
    invoice_item_id UUID NOT NULL REFERENCES {SCHEMA_NAME}.invoice_items(id) ON DELETE CASCADE,
    tax_type_id UUID NOT NULL,
    tax_type_name VARCHAR(100) NOT NULL,
    percentage NUMERIC(9, 4) NOT NULL,
    amount NUMERIC(12, 2) NOT NULL
);
