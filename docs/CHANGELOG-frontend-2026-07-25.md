# Cambios de backend para Frontend — 2026-07-25

Rama: `develop`. Dos cambios pedidos por el equipo de frontend.

---

## 1. Fix: filtros de fecha de un solo día no mostraban datos

**Síntoma reportado:** al filtrar reportes/historiales por un solo día (mismo
valor en `from` y `to`, como envía un selector de fecha simple), no aparecía
ningún resultado.

**Causa:** el backend comparaba `from`/`to` tal cual llegaban, sin expandir el
rango al día completo — `from == to` se traducía literalmente a un rango de 0
segundos. Además, dos de los cinco endpoints todavía tenían un fix más viejo y
más limitado, que solo evitaba el crash de Postgres pero no ajustaba
correctamente la hora de Colombia (UTC-5) contra los timestamps guardados en
UTC.

**Corregido en:**
- `GET /api/cash-register/sessions` (`fromDate`/`toDate`)
- `GET /api/sales` (`from`/`to`)
- `GET /api/reports/cash-flow` (`fromDate`/`toDate`)
- `GET /api/reports/sales-summary` (`fromDate`/`toDate`)
- `GET /api/reports/sales-by-employee` (`fromDate`/`toDate`)

**No cambia el contrato de la API** (mismos parámetros, mismo formato) — solo
cambia qué datos devuelve. Si el frontend tenía algún workaround para este
bug (ej. enviar `to` como el día siguiente a medianoche), ya no es necesario
y puede quitarse: ahora `from=2026-07-25&to=2026-07-25` devuelve el día
completo correctamente.

> Nota: `GET /api/invoices` (filtro de facturas) **no cambió** — ese endpoint
> fue diseñado desde el principio para aceptar fecha *y hora* explícitas (no
> solo el día), así que no aplica esta misma lógica de "día completo".

---

## 2. Nuevo: carga masiva de productos

### `POST /api/inventory/products/bulk` — nuevo

Pensado para que el frontend suba un Excel/CSV completo en un solo request:
mismo formato de fila que `POST /api/inventory/products` (`CreateProductRequest`),
en una lista.

```jsonc
// Request
{
  "products": [
    {
      "sku": "MART-001", "name": "Martillo", "description": "",
      "price": 25000, "cost": 15000, "minStock": 5,
      "categoryId": "...", "unitId": "...",
      "attributes": null,
      "taxes": [ { "taxTypeId": "...", "percentage": 19 } ]
    }
    // ...cientos o miles de filas más
  ]
}
```

```jsonc
// Response (201)
{
  "insertedCount": 998,
  "skippedCount": 2,
  "errors": [
    "SKU 'MART-014' ya existe y fue omitido.",
    "SKU 'MART-057': el precio debe ser mayor a cero."
  ]
}
```

**Reglas importantes para el frontend:**

- **Nunca actualiza productos existentes.** Si una fila trae un SKU que ya
  tiene un producto activo, esa fila se omite (cuenta en `skippedCount`, con
  su mensaje en `errors`) — no hay upsert. Si el usuario necesita corregir
  productos existentes, debe hacerlo con `PUT /api/inventory/products/{id}`,
  no re-subiendo el archivo.
- **Es parcial por diseño:** si 2 de 1000 filas tienen un problema (SKU
  duplicado, categoría/unidad/impuesto inexistente, o un dato inválido como
  precio ≤ 0), esas 2 se omiten y las otras 998 sí se crean — el archivo
  completo nunca se rechaza por un error aislado.
- `errors` es una lista de mensajes en texto plano pensados para mostrarse
  directamente al usuario (no un código de error estructurado por fila).
- Incluye el mismo SKU dos veces en el mismo archivo → solo la primera
  ocurrencia se crea, la segunda se cuenta como omitida.
