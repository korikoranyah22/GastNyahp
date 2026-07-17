# Arquitectura de GastNyahp

## Propósito

GastNyahp es una aplicación web familiar con dos superficies de entrada sobre el mismo dominio:

- una API REST consumida por el frontend React;
- un servidor MCP consumido por agentes como AngelNaira o ChatGPT.

Ambas superficies usan los mismos servicios de aplicación. El frontend y los agentes no escriben directamente
en PostgreSQL ni manipulan aggregates.

## Vista general

```text
Navegador React ──HTTP──┐
                       ├──> GastNyahp.Api ──> Application Services ──> Command Services
Agente MCP ─────MCP────┘            │                    │                    │
                                    │                    │                    v
                                    │                    │              Event Store
                                    │                    │              append-only
                                    │                    │                    │
                                    │                    v                    v
                                    └──────────────> Read Models <── Proyecciones $all
                                                     EF Core
                                                        │
                                                        v
                                                   PostgreSQL
```

## Capas y dependencias

### `GastNyahp.Domain`

Contiene reglas puras y el modelo event-sourced:

- eventos versionados;
- estados inmutables;
- comandos;
- `CommandService<TState>` de Eventuous;
- value objects y algoritmos compartidos.

Depende de Eventuous, pero no de ASP.NET, EF Core ni PostgreSQL.

### `GastNyahp.Infrastructure`

Conecta el dominio con almacenamiento y consultas:

- event store Postgres o en memoria;
- proyecciones Eventuous;
- `ProjectionsDbContext` de EF Core;
- servicios de aplicación;
- migraciones;
- inicializadores y procesos alojados.

Depende de `GastNyahp.Domain`.

### `GastNyahp.Api`

Es el composition root y la superficie de transporte:

- controllers REST;
- middleware de autenticación familiar;
- OAuth para clientes MCP;
- servidor MCP embebido;
- descarga de skills;
- health checks y OpenAPI en desarrollo.

Depende de Domain e Infrastructure. La mayor parte de la lógica permanece fuera de los controllers y tools.

### `app`

SPA en React:

- páginas y componentes visuales;
- cliente HTTP;
- mappers API ↔ UI;
- store Zustand;
- navegación React Router;
- estilos Tailwind CSS.

El backend es la fuente de verdad. Zustand funciona como estado de sesión y caché de lectura.

## Flujo de escritura

1. Un controller REST o una tool MCP recibe una intención.
2. `FamilyAuthMiddleware` resuelve el bearer token y agrega `FamilyId`, principal y rol al `HttpContext`.
3. La superficie llama a un servicio de aplicación.
4. El servicio comprueba invariantes que requieren consultar otros aggregates o read-models.
5. Construye un comando tipado y lo envía al `CommandService` correspondiente.
6. Eventuous reconstruye el estado del stream, ejecuta guards y agrega los eventos resultantes.
7. `CommandExecutor` convierte el resultado en `OpResult` y espera que las proyecciones alcancen el nuevo head.
8. REST traduce `OpResult` a `200` o `422`; MCP devuelve una respuesta conversacional equivalente.

## Flujo de lectura

Las consultas no reconstruyen streams. Los servicios crean un `ProjectionsDbContext` mediante
`IDbContextFactory`, filtran siempre por `FamilyId` cuando corresponde y devuelven entidades del read-model.

## CQRS pragmático

La escritura y la lectura tienen modelos distintos:

- escritura: streams, eventos, estado de aggregate y comandos;
- lectura: tablas EF diseñadas para listas, filtros y joins.

No hay dos aplicaciones separadas ni un bus distribuido. CQRS vive dentro del mismo proceso y la misma base de
datos, con schemas lógicos distintos.

## Multi-tenancy por familia

`FamilyId` es la frontera de aislamiento. Los eventos de creación incluyen la familia y los servicios filtran
las consultas por ella. Las credenciales pueden representar:

- un miembro;
- una sesión de miembro;
- una clave de agente MCP.

Las claves de agente otorgan acceso a datos, no privilegios administrativos familiares.

## Procesos de arranque

`AddGastNyahpInfrastructure` registra:

1. `ProjectionsDbContext`;
2. migración de la base de proyecciones;
3. event store e inicialización de su schema;
4. subscription `$all` o pump en memoria;
5. scheduler de día hábil, si está habilitado;
6. proyecciones, command services y servicios de aplicación.

El orden importa: el schema del event store debe existir antes de iniciar la subscription o escribir eventos.

## Despliegue

El Compose local contiene PostgreSQL, backend y frontend Nginx. Nginx sirve la SPA y proxea `/api`, `/mcp`,
OAuth y, cuando se habilite, hubs de tiempo real. Railway despliega los servicios por separado y conecta frontend
y backend mediante su red privada.

## Estado del tiempo real

Existen `FamilyUpdatesHub` y `RealtimeChangeMiddleware` como base de SignalR, pero actualmente no están
registrados en `Program.cs` ni consumidos por el frontend. Por lo tanto, la arquitectura productiva actual sigue
siendo request/response y requiere recarga para observar escrituras hechas por otro cliente.

## Fuentes relacionadas

- [Event sourcing y Eventuous](EVENT_SOURCING.md)
- [Comandos y servicios](COMMANDS_AND_SERVICES.md)
- [Proyecciones y persistencia](PROJECTIONS_AND_PERSISTENCE.md)
- [API, MCP y frontend](INTERFACES.md)
- [Desarrollo y pruebas](DEVELOPMENT.md)
- [Modelo de dominio detallado](DOMAIN_MODEL.md)
