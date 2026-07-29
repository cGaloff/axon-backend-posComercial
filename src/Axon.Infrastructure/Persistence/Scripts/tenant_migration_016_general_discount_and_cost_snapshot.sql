-- Migración 016: descuento general de venta, descuento por % en productos, y
-- snapshot de costo por línea
-- Reportado por negocio: hasta ahora solo existía descuento por producto en
-- monto fijo (sale_items.discount). Se agrega:
--   1. Descuento general a TODA la venta (monto o %), repartido
--      proporcionalmente entre los productos (sale_items.general_discount_share)
--      para que el impuesto de cada línea se recalcule correctamente. El monto
--      ya convertido queda también en sales/invoices.general_discount_amount,
--      solo para mostrarlo como una línea propia en la factura.
--   2. El descuento manual por producto ahora también se puede dar como %
--      (sale_items.discount_percentage), guardado solo para mostrarlo junto
--      al monto en la factura ("Desc: (5%)") — el monto ya convertido sigue
--      viviendo en discount.
--   3. Costo unitario snapshoteado al momento de la venta (sale_items.unit_cost,
--      copiado de products.cost) — necesario para el nuevo reporte de ganancia
--      (GetProfitReportQuery): sin este snapshot, la ganancia de una venta
--      pasada cambiaría retroactivamente cada vez que se actualice el costo
--      promedio ponderado del producto.
--
-- Es IDEMPOTENTE: las columnas se agregan con IF NOT EXISTS y DEFAULT 0 (o
-- NULL para los campos de solo-presentación), así que las ventas/facturas ya
-- existentes quedan sin descuento general, sin % de descuento a mostrar, y con
-- costo en cero (sin dato histórico) — no requieren backfill.

ALTER TABLE {SCHEMA_NAME}.sales
    ADD COLUMN IF NOT EXISTS general_discount_amount NUMERIC(12, 2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS general_discount_percentage NUMERIC(9, 4);

ALTER TABLE {SCHEMA_NAME}.sale_items
    ADD COLUMN IF NOT EXISTS discount_percentage NUMERIC(9, 4),
    ADD COLUMN IF NOT EXISTS general_discount_share NUMERIC(12, 2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS unit_cost NUMERIC(12, 2) NOT NULL DEFAULT 0;

ALTER TABLE {SCHEMA_NAME}.invoices
    ADD COLUMN IF NOT EXISTS general_discount_amount NUMERIC(12, 2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS general_discount_percentage NUMERIC(9, 4);

ALTER TABLE {SCHEMA_NAME}.invoice_items
    ADD COLUMN IF NOT EXISTS discount_percentage NUMERIC(9, 4),
    ADD COLUMN IF NOT EXISTS general_discount_share NUMERIC(12, 2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS unit_cost NUMERIC(12, 2) NOT NULL DEFAULT 0;
