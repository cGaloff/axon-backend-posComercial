# Respuesta del backend a `BACKEND_REQUESTS-frontend-2026-11-11.md` — 2026-11-12

Rama: `feature/portal-cuenta-suscripcion`.
Shape de siempre: `{ "success", "message", "data", "errors" }`, más `code` cuando aplica (ver 1.1).

| # | Pedido | Respuesta |
|---|---|---|
| 1.1 | `code` en los 403 | **Implementado** |
| 1.2 | Contrato de `refresh` | Confirmado, con dos detalles (ver abajo) |
| 1.3 | Política de contraseña | **Cambiado:** 8 caracteres en todos los flujos |
| 1.4 | Contrato de usuarios | Confirmado |
| 1.5 | Roles en el portal | Respuesta con la tabla de permisos |
| 1.6 | Valores de `subscription/status` | Confirmado |
| 1.7 | `trial-info` público | **Implementado** |
| 1.8 | Logout con revocación | Ya existía |
| 1.9 | Enlace de reset | Confirmado: `token` y `tenant` |
| 1.10 | CORS | Confirmado; depende del `.env` de prod |

---

## 1.1 `code` en los 403 — implementado

Todo 403 trae `code`:

```jsonc
{ "success": false, "message": "Tenant suspendido.",  "code": "TENANT_SUSPENDED",     "errors": ["Tenant suspendido."] }
{ "success": false, "message": "Suscripción vencida.", "code": "SUBSCRIPTION_EXPIRED", "errors": ["Suscripción vencida."] }
{ "success": false, "message": "No tienes permiso para esta acción.", "code": "FORBIDDEN",
  "errors": ["Permiso requerido: configuration:read"] }
```

- El de permiso cambió de texto: antes era `"No tienes permiso para: configuration:read"`. El permiso exacto ahora va en `errors`.
- **Login de un negocio bloqueado:** ahora siempre es **403** con `code`. Antes, en un caso límite (caché de 5 minutos), la suscripción vencida salía como 400 `"Suscripción vencida"` sin punto. Ese caso ya no existe.
- `TENANT_BLOCKED` no se usa. Pueden dejarlo en el front, no hace daño.
- Las respuestas sin error de este tipo no traen `code` (el campo no aparece, no viene `null`).

La heurística por texto ("suspendid"/"vencid") ya se puede quitar.

## 1.2 `POST /api/auth/refresh` — confirmado

- Ruta, body y respuesta: tal cual los describen. `data` es el mismo de login (`accessToken`, `refreshToken`, `expiresAt`, `userFullName`, `role`, `permissions`, `userId`, `businessName`, `slug`).
- **Requiere `X-Tenant-Slug`.**
- **Rota siempre:** cada refresh revoca el token usado y devuelve uno nuevo. Hay que guardar el nuevo. Reusar el viejo falla.
- Si falla responde **400** (no 401) con `success: false`:
  - `"El refresh token es inválido o expiró. Iniciá sesión de nuevo."`
  - `"El usuario ya no está activo"`
  - `"La sesión expiró por inactividad. Iniciá sesión de nuevo."`: sin actividad durante `Jwt:InactivityTimeoutMinutes` (60 por defecto), aunque el refresh token no haya vencido.
- Si el negocio está bloqueado, responde 403 con `code` (1.1) y no 400. Conviene que el front revise `code` antes de cerrar la sesión.
- **Ojo con peticiones en paralelo:** si dos llamadas reciben 401 a la vez y las dos refrescan con el mismo token, la segunda falla porque el token ya rotó. Hay que compartir una sola promesa de refresh.
- El refresh token dura `Jwt:RefreshTokenExpiresInDays` (7 por defecto).

## 1.3 Contraseñas — 8 caracteres en todo

`POST /api/users`, `POST /api/users/{id}/reset-password`, `change-password` y `reset-password` exigen **8 caracteres**. Si no se cumple: **400** con
`message: "La contraseña debe tener al menos 8 caracteres"`.

Cambio general: en **todos** los errores de validación, `message` trae ahora el primer error legible, no `"Error de validación"`. La lista completa sigue en `errors`.

El login sigue aceptando contraseñas de 6 o más caracteres para que los usuarios que ya existen puedan entrar. `MIN_USER_PASSWORD = 8` está bien.

## 1.4 Usuarios — confirmado

- `GET /api/users?includeInactive=true` devuelve exactamente `id, fullName, email, roleId, roleName, isActive, createdAt`.
- Correo duplicado → **400** `"Ya existe un usuario con ese email"`.
- Rol inexistente → 400 `"El rol especificado no existe"`.
- Sin caché: el `GET` refleja los cambios al instante.

## 1.5 Qué ve cada rol en el portal

Cualquier usuario activo puede iniciar sesión, porque es la misma cuenta del POS. Lo que puede ver depende de sus permisos:

| Rol | `subscription/status` | `GET tenants/profile` | `PUT tenants/profile` | `users`, `users/roles` |
|---|---|---|---|---|
| Propietario | ✔ | ✔ | ✔ | ✔ |
| Administrador | ✔ | ✔ | — | — |
| Auditor | ✔ | ✔ | — | — |
| Cajero | ✔ | — | — | — |
| Bodeguero | ✔ | — | — | — |

— = 403 `FORBIDDEN`.

**Recomendación:** decidir con `permissions` del login (`configuration:read`, `configuration:write`, `users:read`, `users:write`) y no con el nombre del rol. Mostrar u ocultar cada sección según el permiso y no pedir lo que va a dar 403. Si producto quiere el portal solo para el Propietario, basta con bloquear en el front a quien no tenga `users:write`. No hace falta cambiar el backend.

## 1.6 `subscription/status` — confirmado

- `currentBillingCycle`: `"mensual" | "trimestral" | "semestral" | "anual"` (siempre en minúscula) o `null` si nunca pagó por este backend.
- Sin pagos: `amountPaid: 0`, `lastPaymentDate: null`.
- `status`: `"trialing" | "active" | "expired"`.

## 1.7 `GET /api/subscription/trial-info` — nuevo

Público: sin `Authorization` y sin `X-Tenant-Slug`.
```jsonc
{ "success": true, "message": "ok", "data": { "trialDays": 5 } }
```
Sale del mismo `SUBSCRIPTION_TRIAL_DAYS` que se usa al registrar. Ya se puede eliminar `VITE_TRIAL_DAYS`.

## 1.8 `POST /api/auth/logout` — ya existía

Body `{ "refreshToken": "..." }` y `X-Tenant-Slug`. Revoca el token. Es idempotente: responde 200 aunque el token ya no sirva. Llámenlo al salir.

## 1.9 Enlace de restablecer contraseña — confirmado

`{FRONTEND_RESET_PASSWORD_URL}?token=<hex de 64 caracteres>&tenant=<slug>`. El token es hexadecimal, no necesita decodificarse. Dura `Auth:PasswordResetTokenExpiresInMinutes` (30 por defecto).

## 1.10 CORS — confirmado, con una corrección

La política permite cualquier header y cualquier método (`AllowAnyHeader`/`AllowAnyMethod`) para los orígenes configurados. Corre antes de la resolución de tenant, así que los preflight y los 400/403/404 del middleware también llevan los headers CORS.

**Bug corregido:** hasta ahora `CORS_ALLOWED_ORIGINS` del `.env` no tenía efecto. La API solo aceptaba `https://axonpospymes.com`, que viene fijo en `appsettings.json`. Cualquier otro origen (`www.`, staging, `localhost` contra prod) fallaba por CORS. Ahora se lee la variable como lista separada por comas. **Ops:** al desplegar, revisar que `CORS_ALLOWED_ORIGINS` tenga todos los dominios del frontend, sin barra final. Por ejemplo:
`CORS_ALLOWED_ORIGINS=https://axonpospymes.com,https://www.axonpospymes.com`.

---

## Despliegue

Este cambio no trae migraciones nuevas, ni del master ni de tenants. Solo hay que desplegar el código.
