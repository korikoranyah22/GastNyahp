# Diseño — Cuentas de usuario y login (email + contraseña)

> Estado: **propuesta, sin implementar**. Documento para revisar antes de tocar código.
> Cambia una decisión deliberada del [`DOMAIN_MODEL.md`](DOMAIN_MODEL.md) §17 ("sin emails, sin SMS": posesión = identidad).

## 1. El problema

Hoy la identidad es **posesión de un token**: entrás creando una familia o escaneando un QR, el token queda en el
`localStorage` del navegador, y **no hay forma de volver a entrar**. Si lo borrás, cambiás de dispositivo o se te
va el navegador, quedás afuera **para siempre** — y ni un admin puede recuperarte, porque no hay a quién
identificar. No es un bug: es la consecuencia directa del modelo. Pero es inaceptable para uso real.

## 2. Decisiones tomadas

| Decisión | Elegido | Consecuencia |
|---|---|---|
| Alcance | **Cuentas de usuario reales** (email + contraseña) | Se reemplaza el modelo de posesión §17 |
| Unicidad del email | **Por familia** | Dos familias pueden tener el mismo email; el login necesita desambiguar |
| Convivencia | **Reemplaza**: todo miembro necesita cuenta | Hay que migrar a los miembros existentes |
| Reset | **Sin mail**: lo genera un Admin de la familia | Cero infraestructura SMTP |

### 2.1 Por qué "único por familia" es la opción correcta (y no una concesión)

Los miembros viven **dentro** del `FamilyState` (`Members: IReadOnlyList<FamilyMember>`). Entonces "no puede
haber dos emails iguales en esta familia" es un **invariante de aggregate puro**: se garantiza en el handler,
en el mismo stream, sin leer nada más y **sin carreras**.

La unicidad global, en cambio, no es expresable en este aggregate (cada familia es un stream distinto):
habría que chequear contra el read-model desde el application service —como `CardService` chequea que exista el
banco— y eso deja una ventana de carrera real (dos registros simultáneos con el mismo email pasan los dos).
La elección "por familia" evita esa clase entera de bugs.

## 3. Las tres tensiones y cómo se resuelven

### 3.1 🔴 CRÍTICO — El admin no tiene quién lo resetee

"Reemplaza" + "solo el admin resetea" crea un **bootstrap sin salida**: si el único Admin olvida su contraseña,
**nadie** puede resetearlo y la familia queda inaccesible con todos sus datos adentro. Hoy esto no pasa porque
el token existe con independencia de la memoria de nadie.

**Solución — la llave de la instancia como último recurso.** Ya existe una autoridad por encima de la familia:
`Admin:ApiKey` (`GASTNYAHP_ADMIN_KEY`), la que emite los códigos para crear familias
([`AdminController`](../backend/src/GastNyahp.Api/Controllers/AdminController.cs)). Se le agrega un endpoint gemelo:

```
POST /api/admin/password-resets   (X-Admin-Key)   body: { familyId, email }
  → devuelve un código de reseteo de un solo uso
```

Es el mismo patrón, la misma llave, la misma semántica de "código de un uso" — y quien opera la instancia (vos)
siempre puede destrabar a un admin. Sin esto, **el feature es una trampa**.

### 3.2 El login necesita saber a qué familia entrás

Con unicidad por familia, `miyu@x.com` puede existir en dos familias. Opciones descartadas: pedir el nombre de
la familia (nadie lo recuerda exacto), pedir un código (volvemos a la posesión).

**Solución — desambiguación progresiva (estilo Slack):**

1. `POST /api/families/login { email, password }`.
2. El servidor busca las familias que tienen ese email **y** cuya contraseña coincide.
   - **1 match** → login OK, devuelve el token de sesión. **Es el 99% de los casos.**
   - **0 match** → `401` genérico (ver §6.2).
   - **2+ matches** → `300` con la lista de familias (`[{familyId, name}]`), sin token. El cliente reintenta con
     `{ email, password, familyId }`.

El caso multi-familia es raro y el costo lo paga solo quien lo tiene.

### 3.3 Migración: los miembros que ya existen

Miyu y Secretaria **no tienen** email ni contraseña. Si el login se vuelve obligatorio de un día para el otro,
quedan afuera — el problema que veníamos a resolver.

**Solución — período de gracia, en dos etapas:**

- **Etapa 1 (compatibilidad).** Las credenciales son opcionales. El token viejo **sigue funcionando**. La app
  muestra un cartel persistente: *"Creá tu cuenta para no perder el acceso"* → el miembro setea email+contraseña
  **autenticado con su token actual** (`POST /api/families/me/credentials`). Sin fricción, sin perder a nadie.
- **Etapa 2 (corte).** Cuando todos los miembros de todas las familias tienen credenciales (es verificable con
  una query), se activa `Auth:RequireCredentials=true`: los tokens de miembro sin credenciales dejan de
  resolver, y `join` por QR pasa a **exigir** email+contraseña en el alta.

Sin la etapa 1 esto es un corte destructivo. **El flag es parte del feature, no un extra.**

## 4. El modelo

### 4.1 Hashing de contraseñas — NO reusar `SecretHash`

[`SecretHash.Compute`](../backend/src/GastNyahp.Domain/Common/SecretHash.cs) es **SHA-256 pelado, sin salt**. Es correcto
para lo que hace hoy: los tokens son 32 bytes aleatorios (256 bits de entropía) — la fuerza bruta es inviable y
el salt no aporta.

Para contraseñas **elegidas por humanos** es inseguro:
- **Rápido**: una GPU prueba miles de millones de SHA-256/s. Un diccionario cae en minutos.
- **Sin salt**: dos personas con la misma contraseña dan el mismo hash → rainbow tables, y se filtra quién
  comparte contraseña.

**Clase nueva y separada**, `PasswordHash` (que nadie confunda una con otra):

```
Algoritmo:  PBKDF2-HMAC-SHA256   (Rfc2898DeriveBytes, en el framework — sin dependencias nuevas)
Salt:       16 bytes aleatorios, por contraseña
Iteraciones: 600_000             (recomendación OWASP 2023 para PBKDF2-SHA256)
Salida:     32 bytes
Formato:    "pbkdf2-sha256$<iter>$<salt-b64>$<hash-b64>"   ← el iter viaja adentro: subirlo después no invalida los hashes viejos
Verificación: CryptographicOperations.FixedTimeEquals      ← comparación en tiempo constante
```

> Argon2id sería preferible, pero no está en el framework y agrega una dependencia nativa. PBKDF2 con 600k
> iteraciones es aceptable y es lo que .NET da sin traer nada.

### 4.2 Sesiones (multi-dispositivo)

**Restricción dura**: el token crudo del miembro **no se guarda** (solo su hash), así que el login **no puede
devolver el token existente** — hay que emitir uno nuevo.

Un solo `TokenHash` por miembro (como hoy) significaría que entrar en el celular **desloguea la compu**. Para
uso familiar real hace falta multi-dispositivo → **un set de tokens por miembro**.

Esto ya existe en el repo: es exactamente `FamilyAgentKey(KeyId, Name, TokenHash, Revoked)`. Se calca:

```csharp
public readonly record struct MemberSession(Guid SessionId, Guid MemberId, string TokenHash, string CreatedAt, bool Revoked);
```

El `TokenHash` actual del miembro se mantiene como está (compatibilidad, etapa 1) y las sesiones se suman.
`ResolveCredentialAsync` prueba: miembro → **sesión** → agent key.

### 4.3 Cambios en el aggregate `Family`

```csharp
// State
public readonly record struct FamilyMember(
    Guid MemberId, string Name, MemberRole Role, string TokenHash,
    string? Email = null, string? PasswordHash = null);        // opcionales (etapa 1)

public IReadOnlyList<MemberSession> Sessions { get; init; } = [];
public IReadOnlyList<PasswordReset> PasswordResets { get; init; } = [];
```

| Comando | Evento | Guard |
|---|---|---|
| `SetMemberCredentials(FamilyId, MemberId, Email, PasswordHash)` | `MemberCredentialsSet` | El miembro existe; **el email no lo usa OTRO miembro de esta familia** (el invariante de §2.1); email con formato válido |
| `ChangeMemberPassword(FamilyId, MemberId, PasswordHash)` | `MemberPasswordChanged` | El miembro existe y ya tiene credenciales |
| `IssueMemberSession(FamilyId, MemberId, SessionId, TokenHash, DeviceName?)` | `MemberSessionIssued` | El miembro existe |
| `RevokeMemberSession(FamilyId, SessionId)` | `MemberSessionRevoked` | La sesión existe y no está revocada (idempotente si ya lo está) |
| `IssuePasswordReset(FamilyId, MemberId, ResetId, CodeHash, IssuedBy)` | `PasswordResetIssued` | Quien emite es **Admin de la familia** — o el flujo de instancia (§3.1) |
| `RedeemPasswordReset(FamilyId, ResetId, NewPasswordHash)` | `PasswordResetRedeemed` + `MemberPasswordChanged` | El reset existe, **no fue usado** y no expiró (TTL 48h, igual que las invitaciones) |

**El `PasswordHash` se calcula en el application service, nunca en el aggregate**: el dominio recibe el hash ya
hecho, igual que hoy recibe `TokenHash` y nunca el token crudo. La contraseña en texto plano **no entra jamás**
a un evento — los eventos son inmutables y para siempre.

### 4.4 Proyección + migración EF

- `family_members`: `Email` (nullable), `PasswordHash` (nullable). Índice **único compuesto `(FamilyId, Email)`**
  → el invariante de §2.1 también queda blindado en la DB, no solo en el aggregate.
- `family_member_sessions`: tabla nueva (`SessionId`, `FamilyId`, `MemberId`, `TokenHash` **indexado**,
  `CreatedAt`, `DeviceName`, `Revoked`).
- `family_password_resets`: tabla nueva (`ResetId`, `FamilyId`, `MemberId`, `CodeHash`, `ExpiresAt`, `Redeemed`).

Migración aditiva: columnas nullable + tablas nuevas. **No rompe datos existentes** (§ anti-patrón "NOT NULL sin
default sobre tabla con datos" del skill `ef-migration`).

## 5. Endpoints

| Endpoint | Auth | Qué hace |
|---|---|---|
| `POST /api/families/login` | anónimo | `{email, password[, familyId]}` → `{token}` · `300` si hay varias familias (§3.2) |
| `POST /api/families/me/credentials` | miembro | Setea email+contraseña por primera vez (etapa 1: autenticado con el token viejo) |
| `PUT /api/families/me/password` | miembro | Cambia la contraseña. **Exige la contraseña actual** |
| `GET /api/families/me/sessions` | miembro | Lista sus dispositivos |
| `POST /api/families/me/sessions/{id}/revoke` | miembro | Cierra sesión en un dispositivo |
| `POST /api/families/password-resets` | **Admin** | `{memberId}` → código de un uso (se muestra una vez) |
| `POST /api/families/password-resets/redeem` | anónimo | `{code, newPassword}` → setea la contraseña |
| `POST /api/admin/password-resets` | **X-Admin-Key** | La salida de emergencia de §3.1 |

`login` y `redeem` son anónimos → van agregados a la allowlist del
[`FamilyAuthMiddleware`](../backend/src/GastNyahp.Api/Auth/FamilyAuthMiddleware.cs), que hoy tiene solo `families` y
`families/join`.

## 6. Amenazas

| # | Amenaza | Mitigación |
|---|---|---|
| 1 | **Fuerza bruta al login** | Rate-limit por IP **y por email** (backoff exponencial). Sin esto, PBKDF2 no te salva: te tiran 10k intentos |
| 2 | **Enumeración de usuarios** | `login` y `redeem` devuelven **el mismo 401 genérico** ante email inexistente y contraseña mala. Nunca "ese email no existe" |
| 3 | **Timing attack** | `FixedTimeEquals`. Además, ante email inexistente **igual se corre un PBKDF2 dummy** para no filtrar por tiempo de respuesta |
| 4 | **Contraseñas débiles** | Mínimo 10 caracteres + lista de las 1000 más comunes. Sin límite máximo bajo ni reglas de "una mayúscula y un símbolo" (NIST 800-63B) |
| 5 | **Reset robado** | Código de un uso, TTL 48h, hasheado en reposo (`SecretHash` **sí** sirve: es aleatorio), invalidado al usarse |
| 6 | **Sesión robada** | Revocable por el dueño. Al cambiar la contraseña **se revocan todas las demás sesiones** |
| 7 | **DoS con PBKDF2** | 600k iteraciones ≈ 100ms de CPU: es un vector de DoS. El rate-limit de #1 lo cubre |
| 8 | **Escalada del agente** | Sin cambios: un agent key **no** es miembro → no puede loguearse, ni setear credenciales, ni resetear (ya verificado: `422`) |

## 7. Fases

1. ~~**`PasswordHash` + tests**~~ ✅ — la pieza de seguridad, aislada y testeable sola (vectores conocidos, round-trip, tiempo constante).
2. ~~**Dominio**~~ ✅ — eventos, comandos, guards, `State` + tests de aggregate (incluido "email duplicado en la familia").
3. ~~**Proyección + migración**~~ ✅ — columnas, tablas, índice único compuesto.
4. ~~**Endpoints + rate-limit**~~ ✅ — login, credenciales, sesiones, resets. Verificable por curl.
5. ~~**UI**~~ ✅ — pantalla de login, "creá tu cuenta" en Ajustes, gestión de dispositivos, reset del admin.
6. **Corte** — `Auth:RequireCredentials` recién cuando no queden miembros sin credenciales. **Pendiente.**

> **Estamos en la etapa 1** (§3.3): las credenciales son opcionales y el token viejo sigue entrando. El cartel de
> "creá tu cuenta" ya le insiste a todo miembro sin cuenta, y Ajustes muestra un badge *sin cuenta* por miembro —
> con eso se ve de un vistazo cuándo se puede hacer el corte de la fase 6.

Las fases 1-4 no tocan la app en uso: hasta la 6, todo sigue funcionando igual.

## 8. Qué NO entra

- **Reset por email/SMTP** — decisión explícita: lo emite un Admin (§2).
- **OAuth / login social** — otro feature.
- **2FA** — el modelo de sesiones lo deja abierto, pero no ahora.
- **Roles nuevos** — siguen siendo `Admin` y `Member`.
- **Tocar el agente MCP** — las agent keys quedan exactamente como están.
