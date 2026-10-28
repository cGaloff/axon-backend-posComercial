# Cambios de backend para Frontend — 2026-10-27

Respuesta a `BACKEND_CHANGES_REQUIRED.md` v2.0 (portal de administración).
Rama: `feature/portal-cuenta-suscripcion` (sobre
`merge/vps-suscripcion-en-develop`, que ya unifica prod + develop).

Shape de siempre: `{ "success", "message", "data", "errors" }`. Todo lo
autenticado requiere `Authorization: Bearer` + `X-Tenant-Slug`.

---

## Estado por endpoint del documento

| Endpoint | Estado |
|---|---|
| `POST /api/auth/login` | Ya existía. **Nuevo en `data`:** `userId`, `businessName`, `slug` |
| `POST /api/auth/change-password` | **Nuevo** |
| `POST /api/auth/forgot-password` / `reset-password` | Ya existían, sin cambios |
| `GET/PUT /api/tenants/profile` | **Nuevo** |
| `GET /api/subscription/status` | Ya existía. **Ampliado** con todos los campos del doc |
| `POST /api/subscription/extend` | Ya existía. **Ampliado** (ver abajo) — no es para el frontend |
| `GET /api/users/roles` | Ya existía. **Nuevo:** `isAssignable` |
| `GET/POST/PUT/DELETE /api/users…`, `reset-password`, `reactivate` | Ya existían, sin cambios |
| `/products/{id}/plans`, `/plans/{id}`, `/subscriptions/` | Son del microservicio de pagos (FastAPI), no de este backend |

---

## 1. Login — campos nuevos

```jsonc
"data": {
  "accessToken": "...", "refreshToken": "...", "expiresAt": "...",
  "userFullName": "Juan Pérez", "role": "Propietario", "permissions": [...],
  "userId": "3fa85f64-...",            // nuevo
  "businessName": "Supermercado Don Pedro", // nuevo
  "slug": "mi-negocio"                 // nuevo
}
```
`refresh` devuelve lo mismo. Los errores siguen siendo **400** (no 401 como
dice el doc) con `message` = `"Credenciales inválidas"`, `"Usuario inactivo"`,
`"Suscripción vencida"`, etc.

## 2. `POST /api/auth/change-password` (nuevo)

Cualquier usuario autenticado cambia **su** contraseña (la única, web + POS).
```jsonc
// Request
{ "currentPassword": "ClaveAnterior2026*", "newPassword": "NuevaClave2026*" }
// 200
{ "success": true, "message": "Contraseña actualizada exitosamente tanto para el portal web como para el sistema POS.", "data": "ok" }
```
- `newPassword` mínimo 8 caracteres y distinta de la actual (400 si no).
- Contraseña actual incorrecta → 400 `"La contraseña actual es incorrecta"`.
- No cierra la sesión actual.

## 3. Perfil del establecimiento (nuevo)

### `GET /api/tenants/profile` — permiso `configuration:read`
(Propietario, Administrador, Auditor)
```jsonc
"data": {
  "id": "8d3e7164-...", "businessName": "Supermercado Don Pedro PyME",
  "legalName": "Inversiones Don Pedro S.A.S.", "nit": "901.784.521-3",
  "phone": "+57 301 481 0079", "address": "Calle 72 # 48 - 25, Local 102",
  "city": "Barranquilla", "state": "Atlántico", "taxRegime": "Responsable de IVA",
  "email": "contacto@donpedro.com", "slug": "don-pedro-pyme",
  "ownerName": "Juan Pérez", "createdAt": "2026-08-01T10:00:00Z"
}
```
`legalName`, `nit`, `phone`, `address`, `city`, `state` pueden venir `null`
en negocios que nunca los cargaron.

### `PUT /api/tenants/profile` — permiso `configuration:write` (solo Propietario)
Mismo body del doc. Respuesta `data` = los campos guardados.
- **Obligatorios:** `businessName`, `legalName`, `taxRegime`.
- **`taxRegime` solo acepta** `"Responsable de IVA"` o `"No responsable de IVA"`
  (400 con cualquier otro valor). Es el mismo dato que `isResponsableIva` de
  `/api/tenant-config` y define el IVA en facturas, por eso no tiene default:
  hay que mandarlo siempre.
- `email` no se edita desde acá (sigue en `/api/tenant-config`).

## 4. `GET /api/subscription/status` — ampliado

```jsonc
"data": {
  "isActive": true,
  "status": "active",            // "trialing" | "active" | "expired"
  "plan": "basic",
  "planName": "Plan Todo Incluido PyME",
  "subscriptionExpiresAt": "2026-11-21T23:59:59Z",
  "daysRemaining": 38,
  "trialDaysTotal": 5,
  "isTrial": false,
  "currentBillingCycle": "anual", // null si nunca pagó por este backend
  "amountPaid": 486000,           // último pago; 0 si no hay
  "lastPaymentDate": "2026-10-14T15:30:00Z", // null si no hay
  "nextRenewalDate": "2026-11-21T23:59:59Z"
}
```
Diferencias con el doc, a propósito:
- Durante la prueba `isActive` es **`true`** (el negocio tiene acceso) y
  `isTrial: true` / `status: "trialing"`. Úsenlos para el banner de prueba.
- `daysRemaining` se redondea hacia arriba y nunca es negativo.
- Tenants sin vencimiento (antiguos): `subscriptionExpiresAt`,
  `daysRemaining` y `nextRenewalDate` vienen **`null`** → no mostrar contador.
- La prueba gratis pasa a **5 días** (configurable).

### Bloqueo
Cualquier request (login incluido) de un negocio bloqueado devuelve **403**
con uno de estos `message`:
- `"Tenant suspendido."` — suspendido.
- `"Suscripción vencida."` — venció la fecha de corte.

El interceptor debe redirigir a `/bloqueado` por **status 403**, no por el
texto exacto.

## 5. Roles — `isAssignable`

`GET /api/users/roles` devuelve los roles reales del negocio (Propietario,
Administrador, Cajero, Bodeguero, Auditor) con sus permisos reales
(`sales:write`, `inventory:read`, …). **No existe un rol "Supervisor"** ni
permisos `pos:*`. Nuevo campo `isAssignable`: `false` para Propietario —
no ofrecerlo en los formularios de crear/editar usuario.

## 6. Usuarios — sin `posAccessCode`

`GET /api/users` **no devuelve contraseñas** (ni en claro ni de ningún tipo),
y el backend no las guarda en claro. Las contraseñas solo existen como hash.
Si el propietario necesita darle acceso a un cajero, le asigna una nueva con
`POST /api/users/{id}/reset-password` y se la comunica en ese momento.

## 7. `POST /api/subscription/extend` (webhook de pagos, no frontend)

Header `X-Subscription-Secret`. Body:
```jsonc
{ "slug": "mi-negocio", "billingCycle": "anual", "extensionDays": 365,
  "amount": 486000, "transactionReference": "epayco-ref-98472918" }
```
- `extensionDays` es opcional si viene `billingCycle`
  (mensual 30 · trimestral 90 · semestral 180 · anual 365).
- Si no había vencido, los días se suman a la fecha de vencimiento actual;
  si ya venció, se cuentan desde ahora. Reactiva el tenant y termina la prueba.
- **Idempotente por `transactionReference`:** un reintento responde 200 con
  `message: "La transacción ya había sido aplicada"` sin sumar días otra vez.
- `data`: `{ slug, subscriptionExpiresAt, daysAdded, alreadyProcessed }`.

---

## Despliegue (backend/ops)

1. Migración EF del master `AgregarCicloPruebaYExtensionesSuscripcion` — se
   aplica sola al arrancar.
2. **Migración 018 del schema de cada tenant — manual y obligatoria antes de
   desplegar:**
   ```
   .\scripts\migrate-tax-model.ps1 -MigrationSqlPath ..\src\Axon.Infrastructure\Persistence\Scripts\tenant_migration_018_tenant_profile.sql
   ```
   Sin ella, todo lo que lee `tenant_config` falla con 500 en los tenants
   existentes: tickets de venta, emisión y PDF de facturas, ajustes de stock
   y mermas, y la configuración del negocio.
3. `.env`: `FRONTEND_RESET_PASSWORD_URL=https://axonpospymes.com/reset-password`;
   `SUBSCRIPTION_TRIAL_DAYS=5`; opcional `Subscription__PlanName`.
