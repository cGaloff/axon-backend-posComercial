-- Migración 014: catálogo fijo de impuestos colombianos (TaxCode)
-- El catálogo de tax_types deja de ser editable por el tenant (se eliminan
-- los comandos de crear/editar impuesto): pasa a ser un conjunto cerrado de 8
-- impuestos colombianos con un "code" fijo (TaxCode en el backend) y una
-- descripción larga para mostrar en el frontend. El tenant solo puede seguir
-- desactivando los que no le apliquen a su negocio.
--
-- Es IDEMPOTENTE: los UPDATE solo tocan filas ya sembradas por nombre/code
-- previos, el INSERT usa NOT EXISTS por code, y las columnas se agregan con
-- IF NOT EXISTS.

ALTER TABLE {SCHEMA_NAME}.tax_types
    ADD COLUMN IF NOT EXISTS description VARCHAR(300);

-- Backfill de las 3 filas ya sembradas por el seed anterior (IVA/ICA/INC).
UPDATE {SCHEMA_NAME}.tax_types
    SET code = 'Iva', name = 'IVA', description = 'Impuesto sobre las ventas'
    WHERE code = 'IVA' OR UPPER(name) = 'IVA';

UPDATE {SCHEMA_NAME}.tax_types
    SET code = 'Ica', name = 'ICA', description = 'Impuesto de timbre departamental'
    WHERE code = 'ICA' OR UPPER(name) = 'ICA';

UPDATE {SCHEMA_NAME}.tax_types
    SET code = 'Inc', name = 'INC', description = 'Impuesto nacional al consumo'
    WHERE code = 'INC' OR UPPER(name) = 'INC';

-- Impuestos fijos que faltan por sembrar en tenants ya provisionados antes de
-- este catálogo (comparados por code para que la migración sea idempotente).
INSERT INTO {SCHEMA_NAME}.tax_types (id, code, name, description, is_active)
SELECT gen_random_uuid(), v.code, v.name, v.description, true
FROM (VALUES
    ('Gmf', 'GMF', 'Gravamen a los movimientos financieros'),
    ('IncPl', 'INC PL', 'Impuesto nacional al consumo de bolsas plásticas'),
    ('Incombustible', 'Incombustible', 'Impuesto nacional a la gasolina y al ACPM'),
    ('Incarbono', 'Incarbono', 'Impuesto nacional al carbono'),
    ('Ibua', 'IBUA', 'Impuesto nacional a las bebidas azucaradas')
) AS v(code, name, description)
WHERE NOT EXISTS (
    SELECT 1 FROM {SCHEMA_NAME}.tax_types t WHERE t.code = v.code
);

-- Cualquier fila residual sin code/description (p. ej. un impuesto
-- personalizado creado antes de sellar el catálogo) queda con un valor no
-- nulo en vez de fallar el NOT NULL de abajo.
UPDATE {SCHEMA_NAME}.tax_types SET code = UPPER(name) WHERE code IS NULL;
UPDATE {SCHEMA_NAME}.tax_types SET description = '' WHERE description IS NULL;

ALTER TABLE {SCHEMA_NAME}.tax_types ALTER COLUMN code SET NOT NULL;
ALTER TABLE {SCHEMA_NAME}.tax_types ALTER COLUMN description SET NOT NULL;
