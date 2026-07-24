# Cambios de backend para Frontend — 2026-07-24

Rama: `develop` (commit `53896e3`). Pendiente de PR a `main`.

Este documento resume, de cara al equipo de frontend, todo lo que cambió en el
backend: nuevos endpoints, endpoints modificados (breaking changes) y bugs
corregidos que pueden afectar datos que ya se estaban consumiendo.

> Nota general: ningún cambio de este lote tocó autenticación, permisos ni el
> JWT multi-tenant. Los permisos (`RequirePermission`) que ya usa cada pantalla
> siguen funcionando igual.

---

## 1. Ventas — pagos divididos (breaking change)

Antes una venta tenía **un solo** método de pago y un solo monto. Ahora una
venta puede pagarse con **varios métodos combinados** (ej. mitad efectivo,
mitad tarjeta).

### `POST /api/sales` — payload cambia

```jsonc
// Antes
{
  "items": [...],
  "paymentMethod": "Cash",
  "amountTendered": 50000,
  "cashRegisterId": "..."
}

// Ahora
{
  "items": [
    { "productId": "...", "quantity": 2, "discount": 0 }
  ],
  "payments": [
    { "method": "Cash", "amount": 30000, "amountTendered": 30000 },
    { "method": "Card", "amount": 20000 }
  ],
  "cashRegisterId": "...",
  "customerId": null,
  "customerName": null,
  "customerEmail": null,
  "notes": null
}
```

- `method`: `"Cash" | "Card" | "Transfer" | "Credit"`.
- `amount`: cuánto cubre ese método del total de la venta.
- `amountTendered`: solo aplica a `Cash` (para calcular vueltos); opcional en los demás métodos.
- La suma de `payments[].amount` debe cubrir el total de la venta con una **tolerancia de redondeo de $0.01** (un centavo). Si la diferencia es mayor, el backend rechaza la venta.

### Respuesta de `POST /api/sales` (`ProcessSaleResult`)

```jsonc
{
  "saleId": "...",
  "saleNumber": "...",
  "total": 50000,
  "totalChange": 0,      // suma de vueltos de todos los pagos en efectivo
  "status": "Completed", // o "PendingPayment" si el método requiere confirmación externa
  "invoiceNumber": 123,  // null si status = PendingPayment (factura aún no emitida)
  "pdfReceipt": "..."    // base64/bytes del recibo, null si status = PendingPayment
}
```

### `GET /api/sales` (historial) — `SaleDto` cambia

```jsonc
{
  "id": "...",
  "saleNumber": "...",
  "customerName": "...",
  "status": "Completed",
  "total": 50000,
  "createdAt": "...",
  "itemCount": 3,
  "payments": [
    { "id": "...", "method": "Cash", "amount": 30000, "amountTendered": 30000, "change": 0 },
    { "id": "...", "method": "Card", "amount": 20000, "amountTendered": null, "change": null }
  ],
  "invoiceNumber": 123   // null si la venta aún no tiene factura emitida
}
```

**Acción requerida en frontend:** cualquier pantalla que mostraba
`paymentMethod`/`amountTendered` como campo único (POS, historial de ventas,
recibo) debe iterar `payments[]`.

---

## 2. Factura auditable + PDF descargable desde el historial de ventas

Al completarse el pago de una venta (inmediato o al confirmarse un pago
pendiente) se emite automáticamente una **factura interna** (registro
auditable con numeración consecutiva por tenant, no numeración legal DIAN).

### `GET /api/sales/{id}/invoice` — nuevo

Devuelve el PDF de la factura de esa venta (`application/pdf`). Esto es lo que
permite, desde el historial de ventas, abrir/descargar la factura de una venta
puntual — como se pidió explícitamente.

- 404 si la venta aún no tiene factura (ej. pago pendiente sin confirmar).

### `GET /api/invoices` — nuevo

Listado de facturas con filtro por rango de fecha **y hora** (no solo día).

```
GET /api/invoices?from=2026-07-01T00:00:00&to=2026-07-24T23:59:59&page=1&pageSize=20
```

Respuesta paginada (`PagedResult<InvoiceDto>`):

```jsonc
{
  "id": "...",
  "number": 123,
  "issuedAt": "2026-07-24T15:32:00Z",
  "saleId": "...",
  "saleNumber": "...",
  "customerName": "...",
  "total": 50000,
  "items": [
    {
      "productId": "...", "productName": "...", "productSku": "...",
      "unitPrice": 10000, "quantity": 2, "discount": 0,
      "subtotal": 20000, "subtotalBase": 16806,
      "taxes": [ { "taxTypeId": "...", "taxTypeName": "IVA 19%", "percentage": 19, "amount": 3194 } ]
    }
  ],
  "payments": [ { "method": "Cash", "amount": 30000, "amountTendered": 30000, "change": 0 } ]
}
```

La factura es un **snapshot inmutable**: aunque después se edite el producto
o sus impuestos, la factura ya emitida conserva los valores del momento de la
venta.

---

## 3. Impuestos flexibles por producto (breaking change)

Se eliminó el whitelist fijo de IVA (0%, 5%, 19%). Ahora cada tenant define su
propio catálogo de impuestos y cada producto puede tener **0 a N impuestos**
con el porcentaje que sea.

### `GET /api/inventory/tax-types` — nuevo (catálogo)

```jsonc
[{ "id": "...", "name": "IVA 19%", "code": "IVA19", "isActive": true }]
```

### `POST /api/inventory/tax-types`, `PUT /api/inventory/tax-types/{id}`, `DELETE /api/inventory/tax-types/{id}` — nuevo (CRUD del catálogo)

```jsonc
// POST/PUT body
{ "name": "IVA 19%", "code": "IVA19" }
```

`DELETE` desactiva (no borra), igual que el resto de catálogos del proyecto.

### `POST /api/inventory/products` y `PUT /api/inventory/products/{id}` — nuevo campo `taxes`

```jsonc
{
  "sku": "...", "name": "...", /* ...campos existentes... */,
  "taxes": [
    { "taxTypeId": "...", "percentage": 19 }
  ]
}
```

`taxes` es opcional (producto puede no tener impuestos). Si se omite, el
producto queda sin impuestos configurados.

### `GET /api/inventory/products` y `GET /api/inventory/products/{id}` — `ProductDto` tiene nuevo campo `taxes`

```jsonc
{
  "id": "...", "sku": "...", "name": "...", /* ...campos existentes... */,
  "taxes": [ { "taxTypeId": "...", "taxTypeName": "IVA 19%", "percentage": 19 } ]
}
```

**Acción requerida en frontend:** cualquier UI que asumía un selector fijo de
IVA (0/5/19%) debe reemplazarse por un multi-select contra
`GET /api/inventory/tax-types`, y el formulario de producto debe permitir
agregar/quitar impuestos de esa lista.

---

## 4. Filtros de listado de productos (nuevo)

`GET /api/inventory/products` acepta ahora:

- `minPrice`, `maxPrice` (rango de precio) — **nuevos**.
- `onlyInStock=true` → solo con stock > 0 (ya existía).
- `onlyInStock=false` → **nuevo**: solo agotados (antes este valor no filtraba nada, era un bug).

```
GET /api/inventory/products?minPrice=10000&maxPrice=100000&onlyInStock=true
```

---

## 5. Código de barras por producto (nuevo)

### `GET /api/inventory/products/{id}/barcode` — nuevo

Devuelve una imagen `image/bmp` (Code128) generada al vuelo con el SKU del
producto como contenido — no se persiste, se genera bajo demanda igual que el
recibo PDF. Útil para imprimir etiquetas o mostrar el código en pantalla.

Se usó Code128 (no EAN13) porque el proyecto ya usa SKUs alfanuméricos libres
(ej. `MART-001`), incompatibles con el formato puramente numérico de EAN13.

---

## 6. Reportes nuevos

### `GET /api/reports/sales-by-employee` — nuevo

```
GET /api/reports/sales-by-employee?fromDate=2026-07-01&toDate=2026-07-24
```

```jsonc
{
  "employees": [
    { "userId": "...", "userName": "...", "totalTransactions": 45, "totalRevenue": 2300000 }
  ]
}
```

### `GET /api/cash-register/balance` — nuevo (multi-caja)

Balance consolidado de **todas las cajas con sesión abierta en este momento**
del tenant (las cajas cerradas no entran, ya se contaron al cerrar).

```jsonc
{
  "registers": [
    {
      "cashRegisterId": "...", "cashRegisterName": "Caja 1",
      "sessionId": "...", "openedAt": "...",
      "initialAmount": 100000,
      "totalCashSales": 500000, "totalCreditSales": 0,
      "totalCardSales": 200000, "totalTransferSales": 0,
      "totalManualIncome": 0, "totalExpenses": 20000, "totalReturns": 0,
      "expectedAmount": 580000
    }
  ],
  "totalExpectedAmount": 580000
}
```

> **Pendiente / no implementado todavía:** el reporte de "balance general"
> (segunda parte del mismo prompt) no se implementó — falta que negocio
> confirme la fórmula contable a usar. No asumir que existe un endpoint para
> esto todavía.

---

## 7. Proveedores y compras (breaking changes)

### `POST /api/suppliers` — campos obligatorios nuevos, `NIT` fijo eliminado

```jsonc
// Antes: nit era el único identificador
{ "name": "...", "nit": "900123456-7" }

// Ahora
{
  "name": "...",
  "documentType": "NIT",     // "NIT" | "CC" | "CE" | "Pasaporte"
  "documentNumber": "900123456-7",
  "contactName": "Juan Pérez",   // obligatorio: nombre Y apellido (mínimo 2 palabras)
  "phone": "3001234567",         // obligatorio
  "email": "compras@proveedor.com", // obligatorio, debe tener formato válido
  "address": null,
  "city": null
}
```

Se eliminó el campo de plazo de pago (payment terms) que existía antes — ya
no se envía ni se recibe.

### `GET /api/suppliers/{id}` — nuevo (autocompletado)

Al seleccionar un proveedor en el formulario de compra, el frontend debe
llamar este endpoint para autocompletar el resto de los datos del proveedor
(contacto, teléfono, email, dirección, tipo/número de documento) — el usuario
no debe reingresarlos a mano.

### "Órdenes de compra" ahora se llaman "Compras" de cara al frontend

Las rutas y mensajes cambiaron de naming, pero **la ruta base sigue siendo
distinta a lo que existía antes** — usar las nuevas rutas, no las viejas:

- `GET /api/suppliers/purchases` (antes `purchase-orders`)
- `GET /api/suppliers/purchases/{id}`
- `POST /api/suppliers/purchases`
- `POST /api/suppliers/purchases/{id}/receive`

(Internamente el backend sigue llamando `PurchaseOrder` al tipo — es solo el
naming de cara a UI/URLs el que cambió a "Compras".)

### `POST /api/suppliers/purchases` — campos nuevos

```jsonc
{
  "supplierId": "...",
  "items": [
    { "productId": "...", "quantityOrdered": 10, "unitCost": 5000 }
  ],
  "supplierInvoiceNumber": "FAC-00123",   // nuevo
  "supplierInvoiceDate": "2026-07-20",    // nuevo
  "expectedDate": "2026-07-30",
  "notes": null
}
```

- El campo se llama `quantityOrdered` (cantidad explícita, requerida) — no se infiere de otro campo.
- **No se envían impuestos por línea**: los impuestos de cada producto se toman automáticamente del catálogo `ProductTax` ya configurado en el producto (mismo mecanismo del punto 3) y se snapshotean en la compra.
- El tipo de documento del proveedor (`SupplierDocumentTypeAtPurchase`) queda snapshoteado en la compra al momento de crearla.

### `GET /api/suppliers/purchases/{id}` — `PurchaseOrderDetailsDto` tiene campos nuevos

```jsonc
{
  "id": "...", "supplierId": "...", "supplierName": "...",
  "status": "Pending",
  "orderDate": "...", "expectedDate": "...",
  "supplierInvoiceNumber": "FAC-00123",
  "supplierInvoiceDate": "2026-07-20",
  "supplierDocumentTypeAtPurchase": "NIT",
  "totalOrdered": 50000,
  "items": [
    {
      "purchaseOrderItemId": "...", "productId": "...", "productName": "...", "productSku": "...",
      "quantityOrdered": 10, "quantityReceived": 0, "unitCost": 5000,
      "subtotal": 50000, "taxAmount": 9500, "total": 59500,
      "taxes": [ { "taxTypeId": "...", "taxTypeName": "IVA 19%", "percentage": 19, "amount": 9500 } ]
    }
  ]
}
```

---

## 8. Bugs corregidos (afectan datos ya mostrados en frontend)

Estos no cambian contratos de API, pero cambian **valores** que el frontend
ya estaba mostrando — vale la pena que QA los revise contra pantallas
existentes:

- **Reporte de ventas / historial**: ventas hechas de noche en hora Colombia
  (que ya caían en el día siguiente en UTC) podían desaparecer de los
  reportes filtrados por fecha. Corregido — ahora el filtro convierte
  correctamente el rango de fecha Colombia → UTC antes de consultar.
- **Validación de stock en una venta**: si el mismo producto aparecía en más
  de una línea del carrito, el backend no sumaba correctamente las
  cantidades al validar stock disponible (podía permitir vender más de lo
  que había). Corregido.
- **Reporte de inventario**: ahora incluye `pendingStockAlerts` (productos
  bajo el mínimo) — antes esta alerta no se calculaba en el reporte.
- **No se podía reutilizar el SKU de un producto eliminado** (reportado por
  frontend el 2026-07-24): `DELETE /api/inventory/products/{id}` es un
  soft-delete (el producto queda `isActive: false`, no se borra), pero la
  validación de SKU duplicado en `POST /api/inventory/products` comparaba
  contra todos los productos sin filtrar por activo, así que el SKU de un
  producto ya desactivado quedaba bloqueado para siempre. Corregido: ahora
  solo se valida contra productos activos, así que se puede crear un producto
  nuevo con el mismo SKU de uno ya desactivado.

---

## 9. Resumen de rutas nuevas

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/sales/{id}/invoice` | PDF de la factura de una venta |
| GET | `/api/invoices` | Listado de facturas, filtro por fecha/hora |
| GET | `/api/inventory/tax-types` | Catálogo de impuestos del tenant |
| POST/PUT/DELETE | `/api/inventory/tax-types[/{id}]` | CRUD del catálogo de impuestos |
| GET | `/api/inventory/products/{id}/barcode` | Código de barras (BMP) del producto |
| GET | `/api/reports/sales-by-employee` | Reporte de ventas por empleado |
| GET | `/api/cash-register/balance` | Balance consolidado de cajas activas |
| GET | `/api/suppliers/{id}` | Autocompletado de datos de proveedor |
| GET/POST | `/api/suppliers/purchases[...]` | "Compras" (antes `purchase-orders`) |

## 10. Pendientes (no implementados aún, no asumir que existen)

- Reporte de "balance general" (falta fórmula contable de negocio).
- Renombrar el módulo de "Auditoría" a "Facturación" (prompt aún no recibido).
