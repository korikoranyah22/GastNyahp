# API REST, MCP y frontend

## Una lógica, varias interfaces

REST y MCP son adaptadores. Ambos resuelven una familia autenticada y llaman a los servicios de aplicación de
Infrastructure. Las reglas no deberían duplicarse en controllers, tools ni componentes React.

## API REST

Los controllers usan rutas bajo `/api` y DTOs específicos. Las áreas principales son:

| Ruta base | Responsabilidad |
| --- | --- |
| `/api/admin` | invitaciones administrativas y recuperación de emergencia |
| `/api/families` | creación, unión, login, familia actual, invitaciones, sesiones y claves MCP |
| `/api/banks`, `/api/cards`, `/api/people` | catálogos familiares |
| `/api/installments`, `/api/loans`, `/api/services` | instrumentos y calendarios mensuales |
| `/api/expenses`, `/api/tickets` | gastos simples y compras con ítems |
| `/api/reserves`, `/api/planning` | reservas, presupuestos, ingreso, copia de mes y Dual Pay |
| `/api/drafts` | confirmación y descarte desde la UI |
| `/api/business-day` | apertura y novedades del día |
| `/api/import` | importación legacy y progreso |
| `/api/skill-packages` | manifiesto y descarga pública de skills |

`ApiConventions.ToActionResult` mapea escrituras exitosas a `200`, incluyendo `{ id }` cuando corresponde, y
rechazos del dominio a `422`.

## Autenticación

`FamilyAuthMiddleware` protege toda ruta salvo una allowlist explícita: health/OpenAPI, discovery OAuth, puerta
administrativa, descarga de skills y entradas anónimas de familia.

El bearer token se resuelve contra hashes de:

- credencial original de miembro;
- sesión de login;
- clave de agente.

El middleware agrega al request `FamilyId`, `MemberId` o principal y rol. Controllers y tools obtienen esos
valores mediante extensiones de `HttpContext`.

## OAuth para MCP

La API implementa discovery de protected resource y authorization server, registro dinámico, autorización y
canje de token. Esto permite conectar clientes como ChatGPT sin pegar manualmente una clave de familia en cada
request.

OAuth termina emitiendo una credencial que se resuelve dentro del mismo modelo familiar; no crea un dominio de
datos paralelo.

## Servidor MCP

El servidor vive en el mismo host y se publica en `/mcp` mediante `ModelContextProtocol.AspNetCore`. Las tools se
descubren desde el assembly de la API.

Las tools están separadas por intención:

### Consulta y carga cotidiana

`GastNyahpTools` cubre novedades, registrar/listar gastos, bancos, tarjetas, personas y cuotas pendientes.

### Borradores

`DraftTools` crea y completa borradores, agrega/quita ítems, asigna dueño, lista, confirma y descarta.

### Instrumentos

`InstrumentTools` maneja servicios, préstamos y cuotas: listar, crear, pagar mes, ajustar importe, revisar,
activar, desactivar o finalizar.

### Catálogos y ABM

`CatalogAbmTools` crea y edita bancos, tarjetas y personas, además de activar, desactivar o archivar.

### Reservas y planificación

`ReservePlanningTools` maneja reservas, presupuestos, ingreso, Dual Pay y copia de mes.

### Correcciones

`CorrectionTools` edita gastos, tickets e ítems ya confirmados y expone estado familiar o del día.

La descripción de cada tool es parte del contrato con el modelo: debe explicar cuándo usarla, campos obligatorios
y consultas previas necesarias.

## Skills descargables

Las skills operativas se empaquetan desde `Api/SkillPackages/gastnyahp` y se descargan como ZIP. Están segregadas
en guía general, consultas, ABM e instrumentos. Complementan la descripción de tools con flujos conversacionales,
preguntas iterativas y criterios para no repetir operaciones.

## Frontend React

El frontend tiene cuatro piezas principales:

1. `lib/api.js`: wrapper `fetch`, bearer token y endpoints;
2. `lib/apiMappers.js`: traducción entre contratos del backend y shapes de UI;
3. `store/useStore.js`: estado Zustand, acciones y recargas;
4. `pages/` y `components/`: presentación y formularios.

`App.jsx` inicializa la autenticación y enruta las páginas. `Shell` contiene navegación y layout.

## Estado y sincronización del frontend

El store conserva catálogos, instrumentos, gastos, presupuestos, ingreso, familia y borradores. Las mutaciones:

1. llaman a la API;
2. esperan la respuesta;
3. recargan el slice afectado.

Los gastos se cargan por ventanas de meses porque el ciclo de tarjeta puede impactar un resumen posterior.

Actualmente las escrituras externas —otro familiar o MCP— no invalidan automáticamente el store. Los archivos
base de SignalR existen en el backend, pero falta registrarlos, proxearlos y conectar el cliente React.

## Nginx y rutas públicas

Nginx sirve archivos estáticos y proxea:

- `/api/` al backend;
- `/.well-known/` y `/oauth/` para OAuth;
- `/mcp` sin buffering y con timeout largo;
- rutas SPA a `index.html`.

Cuando SignalR quede habilitado, `/hubs/` deberá soportar `Upgrade: websocket` y fallbacks de SignalR.

## Regla para nuevas interfaces

Una nueva ruta o tool debe:

1. obtener identidad/familia del contexto;
2. validar solo el contrato de transporte;
3. llamar al servicio de aplicación existente o agregar uno tipado;
4. mapear `OpResult` sin reinterpretar la regla;
5. documentar claramente si consulta, prepara borrador o confirma una escritura.

## Referencias

- [Arquitectura](ARCHITECTURE.md)
- [Comandos y servicios](COMMANDS_AND_SERVICES.md)
- [Cuentas y login](ACCOUNTS_AND_LOGIN.md)
