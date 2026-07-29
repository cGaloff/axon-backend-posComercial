-- Migración 015: identificación del cliente (CC/NIT) en la venta y la factura
-- Reportado por negocio: al vender se debe poder pedir la identificación del
-- cliente (CC o NIT), y ese dato debe verse en el historial de ventas y en la
-- factura. Sin documento dado, el campo queda vacío (no tiene default) — solo
-- el nombre del cliente tiene un valor por defecto ("Consumidor Final").
--
-- Es IDEMPOTENTE: las columnas se agregan con IF NOT EXISTS, y el backfill de
-- ventas/facturas históricas solo toca filas que ya estén en blanco.

ALTER TABLE {SCHEMA_NAME}.sales
    ADD COLUMN IF NOT EXISTS customer_document_type VARCHAR(10),
    ADD COLUMN IF NOT EXISTS customer_document_number VARCHAR(30);

ALTER TABLE {SCHEMA_NAME}.invoices
    ADD COLUMN IF NOT EXISTS customer_document_number VARCHAR(30);

-- Ventas/facturas ya existentes sin cliente identificado (venta ocasional de
-- antes de este cambio): se homologan al nombre "Consumidor Final", igual que
-- aplica ahora Sale.Create para las ventas nuevas. El documento no se toca —
-- queda vacío si ya lo estaba.
UPDATE {SCHEMA_NAME}.sales
    SET customer_name = 'Consumidor Final'
    WHERE customer_name IS NULL OR btrim(customer_name) = '';

UPDATE {SCHEMA_NAME}.invoices
    SET customer_name = 'Consumidor Final'
    WHERE customer_name IS NULL OR btrim(customer_name) = '';
