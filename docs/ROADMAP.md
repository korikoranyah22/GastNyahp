# Roadmap vigente de GastNyahp

> Última revisión: 2026-07-16. Este roadmap se basa en brechas observables del código actual. No es un registro
> histórico ni una promesa de fechas. El orden puede cambiar, pero cada ítem debe conservar un criterio de cierre
> comprobable.

## Estado actual

El núcleo funcional ya está implementado:

- familias, invitaciones, roles y claves de agente;
- login con email/contraseña, sesiones y recuperación;
- bancos, tarjetas, personas, cuotas, préstamos y servicios;
- gastos simples, tickets, reservas, presupuestos e ingreso;
- importación legacy;
- servidor MCP, OAuth y skills descargables;
- borradores conversacionales confirmables;
- Eventuous/PostgreSQL, proyecciones EF y read-your-writes;
- frontend responsive y despliegue separado en Railway.

El trabajo siguiente se concentra en sincronización, limpieza técnica, confiabilidad y operación.

## P0 — Actualizaciones en tiempo real

### Situación

Existen `FamilyUpdatesHub` y `RealtimeChangeMiddleware`, pero no están registrados en `Program.cs`. Nginx no
proxifica `/hubs/` y React no inicia una conexión SignalR. Los cambios de otro familiar o de un agente MCP solo
aparecen después de refrescar.

### Alcance

- registrar SignalR y mapear el hub;
- autenticar la conexión con la credencial familiar;
- publicar invalidaciones por familia después de escrituras exitosas REST/MCP;
- configurar WebSocket y fallbacks en Nginx/Railway;
- conectar React y recargar solamente los slices afectados;
- agrupar ráfagas para evitar tormentas de requests;
- cubrir aislamiento entre familias, reconexión y logout.

### Criterio de cierre

Dos navegadores de la misma familia y un agente MCP observan una carga sin refresh manual; otra familia no recibe
la notificación.

## P0 — Retirar infraestructura local-first obsoleta

### Situación

El frontend todavía conserva `driveSync.js`, `mergeUtils.js`, `dirtyTracker.js`, `autoSave.js`, hooks asociados,
seed data y componentes de conflictos. El backend ya es la fuente de verdad y esos módulos pertenecen a la etapa
Google Drive/P2P.

### Alcance

- confirmar qué módulos no tienen ruta activa;
- eliminar hooks, componentes y utilidades no usadas;
- retirar referencias de welcome/settings que dependan del modelo anterior;
- conservar exportación/importación solo donde siga siendo una función explícita;
- actualizar especificación funcional y comentarios legacy.

### Criterio de cierre

No quedan imports ni flujos de Drive/merge/autosave en el bundle productivo y todas las pantallas actuales siguen
funcionando contra la API.

## P1 — Historial de precios completo en backend

### Situación

La pantalla calcula tendencias en el navegador sobre los gastos que Zustand tiene cargados. El store mantiene una
ventana limitada de meses, por lo que “historial” no representa necesariamente toda la historia familiar.

### Alcance

- definir una consulta/proyección de evolución por producto y comercio;
- normalizar descripciones sin perder el texto original;
- exponer búsqueda, rango temporal y ocurrencias;
- evitar cargar todos los tickets en el navegador;
- decidir explícitamente si la métrica usa precio del ítem, unidad/cantidad o importe cargado.

### Criterio de cierre

La pantalla devuelve el mismo historial completo independientemente del mes actualmente cargado en Zustand y
funciona con un volumen grande sin descargar todos los tickets.

## P1 — Idempotencia y trazabilidad de operaciones MCP

### Situación

Varias tools evitan duplicados por nombre y los drafts conservan historial, pero una repetición del mismo tool
call no tiene una clave de idempotencia transversal. Un retry del cliente puede volver a ejecutar una intención.

### Alcance

- introducir `operationId` opcional en escrituras MCP críticas;
- persistir o proyectar el resultado asociado a esa operación;
- devolver el resultado anterior ante retry idéntico;
- rechazar reutilización con parámetros incompatibles;
- incluir principal, tool y operation id en logs estructurados;
- probar retries, timeouts y reconexiones.

### Criterio de cierre

Repetir una escritura con el mismo `operationId` crea una sola entidad y devuelve consistentemente el mismo id.

## P1 — Operación y observabilidad

### Situación

Existe liveness, logging y migración automática, pero falta una superficie clara de readiness y métricas de las
dos rutas de persistencia.

### Alcance

- health/readiness de PostgreSQL, schema Eventuous y proyecciones;
- métricas de lag del checkpoint `$all`;
- métricas de comandos rechazados, errores MCP y latencia de catch-up;
- correlación de request, comando y tool call;
- runbook de replay/rebuild de proyecciones;
- revisar y actualizar `DEPLOY_RAILWAY.md` contra la configuración vigente.

### Criterio de cierre

Un operador puede distinguir proceso vivo, base disponible y proyecciones atrasadas sin inspeccionar manualmente
la base de datos.

## P2 — Escalabilidad de autenticación y realtime

### Situación

`LoginThrottle` y `OAuthFlowStore` mantienen estado en memoria. Es suficiente para una instancia, pero no para
varias réplicas. SignalR también requerirá coordinación si el backend escala horizontalmente.

### Alcance

- mover throttling y estados OAuth temporales a un store compartido o documentar single-instance como límite;
- definir expiración y limpieza de estados OAuth;
- incorporar backplane de SignalR si se habilitan réplicas;
- revisar rate limits de login, OAuth y MCP;
- mantener secretos exclusivamente en variables del entorno.

### Criterio de cierre

Dos instancias del backend aplican los mismos límites y completan OAuth/realtime sin depender de afinidad de
sesión, o el despliegue declara y controla formalmente una sola réplica.

## P2 — Rendimiento y modularización del frontend

### Situación

El build actual genera un bundle principal superior al umbral de advertencia de Vite. Todas las páginas se
importan de forma eager desde `App.jsx`.

### Alcance

- lazy loading por ruta;
- separar dependencias pesadas y formularios secundarios;
- medir carga inicial y navegación;
- evitar recargas completas del store después de mutaciones o eventos realtime;
- mantener estados de carga y error por slice.

### Criterio de cierre

El build deja de advertir por el chunk principal o existe un presupuesto documentado respaldado por mediciones.

## P2 — Integración continua

### Situación

No hay workflows de CI en el repositorio.

### Alcance

- restore/build del backend;
- tests de dominio, integración y E2E;
- lint/build del frontend;
- validación de enlaces Markdown;
- construcción de Dockerfiles;
- escaneo básico de secretos y dependencias.

### Criterio de cierre

Cada cambio se valida automáticamente con las mismas comprobaciones mínimas usadas para un despliegue.

## Criterios para incorporar trabajo nuevo

Un ítem nuevo debe indicar:

1. problema observable;
2. alcance y exclusiones;
3. impacto en dominio, proyecciones, API/MCP, frontend y skills;
4. migración o compatibilidad requerida;
5. criterio de cierre verificable.

Las ideas históricas que ya fueron implementadas o descartadas no vuelven a este archivo; permanecen en los
documentos `HISTORICAL_*`.
