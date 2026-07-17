# Skills de GastNyahp — frontend maqueta + backend real event-sourced

Suite de [skills de Claude Code](https://docs.claude.com/en/docs/claude-code/skills) de este repo. A diferencia
de una suite exportable genérica, esta está **adaptada a este proyecto concreto**: GastNyahp, una app de finanzas
personales cuyo frontend (`app/`, React + Vite + Zustand + Tailwind) ya funciona como maqueta sobre
`localStorage`, y que ahora se conecta a un backend real (`backend/`, .NET + Eventuous + PostgreSQL, en Docker,
con un servidor MCP) siguiendo el mismo dominio que el frontend ya implementa.

Claude Code descubre automáticamente cualquier carpeta bajo `.claude/skills/` con un `SKILL.md` con frontmatter
válido — no hace falta registrar nada.

## Índice de skills

### Dominio (empezar siempre por acá)

| Skill | Cuándo se usa |
|---|---|
| [gastnyahp-domain-model](gastnyahp-domain-model/SKILL.md) | El QUÉ del dominio: tabla canónica de aggregates, eventos, comandos e invariantes de GastNyahp. Consultar antes de tocar cualquier aggregate/evento/proyección nuevos. |

### Backend — .NET + Eventuous + PostgreSQL + MCP

| Skill | Cuándo se usa |
|---|---|
| [eventuous-event-sourced-aggregate](eventuous-event-sourced-aggregate/SKILL.md) | Modelar un aggregate event-sourced (Eventuous): eventos, State, comandos, CommandService con handlers estáticos. |
| [eventuous-projection-readmodel](eventuous-projection-readmodel/SKILL.md) | Proyectar los eventos de un aggregate a una tabla EF Core de lectura (read-model), con idempotencia. |
| [application-service-layer](application-service-layer/SKILL.md) | El service que orquesta comando (escritura) + read-model (lectura) detrás de una sola API para el controller/tool MCP. |
| [ef-core-postgres-context](ef-core-postgres-context/SKILL.md) | Diseñar un DbContext EF Core/Npgsql, multi-contexto, y generar migraciones. |
| [aspnet-rest-endpoint](aspnet-rest-endpoint/SKILL.md) | Agregar un endpoint REST en un controller ASP.NET Core con las convenciones de error/DI del stack. |
| [csharp-conventions-and-patterns](csharp-conventions-and-patterns/SKILL.md) | Convenciones transversales de C#: DI por módulo, Options pattern, records, primary constructors, guard clauses, IHostedService. |
| [http-resilience-polly](http-resilience-polly/SKILL.md) | Agregar retry con Polly a un HttpClient saliente hacia otro servicio de la red. |
| [docker-compose-service-network](docker-compose-service-network/SKILL.md) | Diseñar/extender el docker-compose (Postgres + backend + frontend + MCP), red interna, healthchecks, Dockerfiles multi-stage, proxy nginx. |
| [mcp-tool-server](mcp-tool-server/SKILL.md) | Exponer las funcionalidades de la app como tools de un servidor MCP (SDK oficial), para clientes de IA externos — incluye el patrón de polling de "novedades del día" (BusinessDay). |

### Testing BDD / end-to-end — Reqnroll (sucesor de SpecFlow)

| Skill | Cuándo se usa |
|---|---|
| [reqnroll-gherkin-features](reqnroll-gherkin-features/SKILL.md) | Escribir features Gherkin en español y step definitions con Reqnroll — convenciones de .feature, bindings con inyección de contexto, hooks. |
| [reqnroll-e2e-api-tests](reqnroll-e2e-api-tests/SKILL.md) | El harness E2E: WebApplicationFactory + event store InMemory + SQLite compartida para verificar flujos HTTP → dominio → proyección → base de datos, especialmente invariantes cross-aggregate. |

### Frontend — React + Zustand + Tailwind (la maqueta actual)

| Skill | Cuándo se usa |
|---|---|
| [react-component-patterns](react-component-patterns/SKILL.md) | Estructura de páginas/forms/componentes, hooks que envuelven singletons, routing. |
| [zustand-store-patterns](zustand-store-patterns/SKILL.md) | El store global actual (100% local en `localStorage`): acciones, ids, timestamps, validación por retorno. |
| [react-feature-module](react-feature-module/SKILL.md) | Migrar un slice del store local a datos reales del backend: cliente API tipado → store → hook de carga. |
| [tailwind-ui-system](tailwind-ui-system/SKILL.md) | Paleta dark, recetas de componentes (botones, badges, inputs), layout responsive. |

### Transversales

| Skill | Cuándo se usa |
|---|---|
| [project-conventions](project-conventions/SKILL.md) | Comentarios, tooling (ESLint/Vite), formatters centralizados, filosofía anti-sobre-ingeniería — aplica a front y back. |
| [skill-author](skill-author/SKILL.md) | Meta-skill para seguir agregando/editando skills de este proyecto en este mismo formato. |

## Cómo se relacionan (flujo típico de un feature nuevo de backend)

```
gastnyahp-domain-model                (¿qué aggregate/evento es esto?)
            │
            ▼
eventuous-event-sourced-aggregate   (comandos + eventos + State)
            │
            ▼
eventuous-projection-readmodel      (read-model EF que escucha los eventos)
            │
            ▼
ef-core-postgres-context            (DbContext + migración de la tabla)
            │
            ▼
application-service-layer           (orquesta comando + lectura detrás de una API)
            │
      ┌─────┴─────┐
      ▼           ▼
aspnet-rest-endpoint      mcp-tool-server
(consumo HTTP normal)     (consumo por un agente/cliente de IA)
      │
      ▼
react-feature-module (migra el slice del frontend del store local al backend)
```

`csharp-conventions-and-patterns`, `http-resilience-polly`, `docker-compose-service-network` y
`project-conventions` son transversales — aplican en cualquier punto del flujo de arriba.
`zustand-store-patterns`/`react-component-patterns`/`tailwind-ui-system` documentan la maqueta actual del
frontend, vigente para lo que todavía no migró a `react-feature-module`.

## Notas

- No incluye nada de agentes/LLM propios, Ollama ni Qdrant — GastNyahp no tiene esos componentes; el servidor MCP
  expone la app como *herramienta* para un cliente de IA externo, no al revés.
- El detalle exhaustivo de reglas de negocio (fórmulas, edge cases, decisiones tomadas vs. el frontend actual)
  vive en `backend/docs/DOMAIN_MODEL.md`, no en las skills — las skills enlazan a ese documento cuando hace
  falta el detalle completo.
