# Desarrollo, lenguajes y pruebas

## Stack

### Backend

- C# sobre .NET 10;
- ASP.NET Core controllers y middleware;
- Eventuous 0.16.4;
- Eventuous PostgreSQL y subscriptions;
- EF Core 10;
- Npgsql/PostgreSQL;
- SQLite para pruebas aisladas;
- Model Context Protocol para las tools;
- xUnit y Reqnroll.

### Frontend

- JavaScript con módulos ES;
- React 19;
- Vite 7;
- Zustand 5;
- React Router 7;
- Tailwind CSS 4;
- Lucide y QRCode React.

### Operación

- Docker y Docker Compose;
- Nginx para servir/proxear el frontend;
- PostgreSQL 16;
- Railway como objetivo de despliegue documentado.

## Organización del código

```text
backend/src/
  GastNyahp.Domain/          eventos, states, comandos y command services
  GastNyahp.Infrastructure/  Eventuous, EF, proyecciones y servicios
  GastNyahp.Api/             REST, auth, OAuth, MCP y composition root
backend/tests/
  GastNyahp.Domain.Tests/       reglas puras y guards
  GastNyahp.Integration.Tests/  event store + proyecciones + servicios
  GastNyahp.E2E.Tests/          API in-process y escenarios Reqnroll
app/src/
  components/ pages/ hooks/ lib/ store/
```

## Convenciones C#

- Nullable reference types e implicit usings habilitados.
- `record` para comandos, eventos y resultados inmutables.
- Primary constructors para DI cuando mejoran la legibilidad.
- `DomainException` para guards del aggregate.
- `OpResult` para rechazos esperables entre servicios e interfaces.
- Métodos async con `CancellationToken`.
- Registro de dependencias agrupado en extension methods.
- Inicialización de infraestructura con `IHostedService`, no SQL suelto en controllers.
- Fechas de eventos serializadas en ISO-8601; meses en `yyyy-MM`; días en `yyyy-MM-dd`.

## Convenciones React

- Componentes funcionales.
- Estado remoto centralizado en Zustand.
- Acceso HTTP mediante `lib/api.js`.
- Transformaciones de contrato en `apiMappers.js`.
- Formularios y componentes no escriben `localStorage` ni hacen fetch arbitrario.
- El backend conserva la autoridad sobre reglas y persistencia.

## Comandos habituales

Desde la raíz del repositorio:

```powershell
dotnet restore backend/src/GastNyahp.Api/GastNyahp.Api.csproj
dotnet build backend/src/GastNyahp.Api/GastNyahp.Api.csproj
dotnet test backend/tests/GastNyahp.Domain.Tests/GastNyahp.Domain.Tests.csproj
dotnet test backend/tests/GastNyahp.Integration.Tests/GastNyahp.Integration.Tests.csproj
dotnet test backend/tests/GastNyahp.E2E.Tests/GastNyahp.E2E.Tests.csproj
```

Frontend:

```powershell
cd app
npm ci
npm run dev
npm run lint
npm run build
```

Stack completo:

```powershell
Copy-Item .env.example .env
docker compose up --build
```

## Migraciones EF Core

El proyecto de migraciones es Infrastructure y el startup project es Api:

```powershell
dotnet ef migrations add NombreDeMigracion `
  --project backend/src/GastNyahp.Infrastructure/GastNyahp.Infrastructure.csproj `
  --startup-project backend/src/GastNyahp.Api/GastNyahp.Api.csproj `
  --context ProjectionsDbContext `
  --output-dir Migrations/Projections
```

La design-time factory necesita `ConnectionStrings__Projections` o variables Postgres equivalentes. No se deben
guardar credenciales reales en el comando ni en archivos versionables.

## Estrategia de pruebas

### Domain tests

Ejecutan handlers y algoritmos sin HTTP ni base productiva. Deben cubrir:

- guards;
- transiciones de state;
- generación y revisión de calendarios;
- value objects;
- políticas de contraseña y billing.

### Integration tests

Construyen Domain + Infrastructure con event store en memoria y read-model SQLite. Verifican:

- comando → evento → proyección;
- read-your-writes;
- ownership e invariantes cruzadas;
- replay y consistencia de calendarios.

### E2E tests

Usan `WebApplicationFactory<Program>` y sustituyen infraestructura externa por variantes aisladas. Reqnroll
expresa escenarios en lenguaje de negocio para familias, bancos, cuotas, gastos, MCP, importación y planificación.

También hay pruebas directas para OAuth y paquetes de skills.

## Checklist para una feature vertical

1. Actualizar el modelo de dominio y sus tests.
2. Crear/ajustar comando, evento y state.
3. Actualizar entidad y proyección idempotente.
4. Crear migración si cambia el read-model.
5. Agregar método al servicio de aplicación.
6. Exponer REST y/o MCP.
7. Actualizar API client, mapper y store del frontend.
8. Agregar escenarios de integración/E2E.
9. Actualizar documentación y, si aplica, skills descargables.
10. Ejecutar build, lint y suites afectadas.

## Skills locales como guías

`.claude/skills/` contiene patrones mantenidos junto al proyecto. Las más relevantes son:

- `eventuous-event-sourced-aggregate`;
- `eventuous-projection-readmodel`;
- `application-service-layer`;
- `ef-core-postgres-context`;
- `aspnet-rest-endpoint`;
- `mcp-tool-server`;
- `react-feature-module`;
- `zustand-store-patterns`;
- `reqnroll-e2e-api-tests`;
- `docker-compose-service-network`.

Las skills explican cómo extender el sistema; estos documentos describen cómo está armado GastNyahp hoy. Ante
una diferencia, el código y las pruebas actuales tienen prioridad.

## Referencias

- [Arquitectura](ARCHITECTURE.md)
- [Event sourcing](EVENT_SOURCING.md)
- [Proyecciones](PROJECTIONS_AND_PERSISTENCE.md)
- [Interfaces](INTERFACES.md)
