---
name: reqnroll-e2e-api-tests
description: Harness de tests end-to-end de GastNyahp con Reqnroll + WebApplicationFactory — levantar la API real en memoria (event store InMemory + SQLite compartida) y verificar flujos completos HTTP → application service → aggregate → proyección → base de datos. Usar al crear/tocar el harness E2E o al decidir cómo verificar un flujo que cruza aggregates; la redacción de features/steps está en reqnroll-gherkin-features.
---

# reqnroll-e2e-api-tests

El harness que hace que un escenario Gherkin ejercite la app REAL de punta a punta sin Postgres ni Docker:
`WebApplicationFactory<Program>` levanta `GastNyahp.Api` completo en memoria (controllers → application
services → CommandServices de Eventuous → event store → proyecciones → `ProjectionsDbContext`), con dos
sustituciones de infraestructura vía configuración — el event store InMemory y SQLite in-memory compartida.
Todo lo demás es el código de producción.

## Cuándo usar / cuándo no

- **Usar**: verificar un flujo de negocio completo por HTTP, especialmente **invariantes cross-aggregate**
  (no borrar banco con tarjetas, USD requiere CCL configurado, copiar mes no pisa datos) — esas reglas viven
  en los application services y NINGÚN test de capa inferior las cubre de verdad.
- **No usar**: reglas de un solo aggregate (eso es un unit test del dominio, 100x más rápido) ni semántica de
  una proyección puntual (eso es `GastNyahp.Integration.Tests` con su host propio). La pirámide: dominio >
  integración > E2E — acá van POCOS escenarios, los que cruzan capas.

## Las dos sustituciones (y por qué son seguras)

1. **Event store**: `EventStore:Provider = InMemory` → `InMemoryEventStore` (vive en
   `GastNyahp.Infrastructure/EventStore`, el mismo que usan los integration tests) + el pump `IReadModelSync`
   que proyecta síncrónicamente tras cada comando. Seguro porque los aggregates/handlers son idénticos; lo
   único no cubierto es el SQL del event store Postgres de Eventuous (se cubre al levantar docker-compose).
2. **Read model**: `Database:Provider = Sqlite` con una base in-memory **nombrada y compartida**
   (`Data Source=e2e-{guid};Mode=Memory;Cache=Shared`) — compartida para que los `DbContext` de vida corta del
   `IDbContextFactory` vean la misma base; nombrada con guid para aislar cada escenario; y con una conexión
   keep-alive abierta en el factory porque la base muere cuando se cierra la última conexión.

## El factory + el world

```csharp
// Support/GastNyahpApiFactory.cs
public sealed class GastNyahpApiFactory : WebApplicationFactory<Program>
{
    readonly SqliteConnection _keepAlive;
    public GastNyahpApiFactory()
    {
        var dbName = $"e2e-{Guid.NewGuid():N}";
        ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();
    }

    public string ConnectionString { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("E2E");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Projections", ConnectionString);
        builder.UseSetting("EventStore:Provider", "InMemory");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _keepAlive.Dispose();
    }
}
```

```csharp
// Support/E2EWorld.cs — una instancia POR ESCENARIO (Reqnroll la crea/inyecta/dispone solo)
public sealed class E2EWorld : IDisposable
{
    readonly GastNyahpApiFactory _factory = new();
    readonly Dictionary<(string Kind, string Name), Guid> _ids = [];

    public HttpClient Client { get; }
    public HttpResponseMessage? LastResponse { get; set; }

    public E2EWorld() => Client = _factory.CreateClient();

    public void RememberId(string kind, string name, Guid id) => _ids[(kind, name)] = id;
    public Guid IdOf(string kind, string name) => _ids[(kind, name)];

    public async Task<Guid> PostAndGetId(string url, object body)
    {
        LastResponse = await Client.PostAsJsonAsync(url, body);
        LastResponse.EnsureSuccessStatusCode();
        var payload = await LastResponse.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("id").GetGuid();
    }

    public void Dispose() { Client.Dispose(); _factory.Dispose(); }
}
```

Reglas del harness:
1. **Una app + una base nuevas por escenario** (el world crea su factory) — el aislamiento sale gratis, sin
   hooks de limpieza ni orden entre escenarios. Levantar el host en memoria tarda milisegundos.
2. **Aserciones por la API, no contra la base**: `Entonces el banco existe` = GET al endpoint. Solo consultá
   `ProjectionsDbContext` directo cuando el escenario verifica algo que la API deliberadamente no expone
   (raro; documentalo en el step).
3. Los POST devuelven `{ id }` — el world mantiene el mapa (tipo, nombre-de-negocio) → id para que los
   features hablen por nombre (ver [[reqnroll-gherkin-features]]).
4. El shape de error de la API es texto con el mensaje de dominio en `422 UnprocessableEntity` (ver
   [[aspnet-rest-endpoint]]) — los `Entonces la operación es rechazada con "..."` assertean contra ese texto.

## Qué queda para el modo Postgres real

Cuando el stack Docker esté arriba (ver [[docker-compose-service-network]]), la MISMA suite puede correr
contra la infraestructura real cambiando configuración, no código: `Database:Provider=Postgres` +
`EventStore:Provider=Postgres` (cuando se agregue el paquete `Eventuous.Postgresql` y su subscription).
Los gaps que recién ahí se cubren: SQL/tipos de Npgsql, el catch del `23505` en proyecciones, y la
subscription `$all` real con checkpoint. No dupliques escenarios para eso — es la misma suite con otro
provider.

## Procedimiento

1. ¿El flujo cruza aggregates o capas? Si no, bajá de nivel (dominio o integración) — no infles la suite E2E.
2. Escribí el escenario y los steps según [[reqnroll-gherkin-features]].
3. Si falta un helper del world (otro shape de POST, un GET tipado), agregalo al world — no dupliques
   `HttpClient` plumbing en los steps.
4. `dotnet test` sobre el proyecto E2E.

## Anti-patrones

- ❌ Mockear application services o command services "para que sea más rápido" — entonces ya no es E2E; la
  única infraestructura sustituible es la de las dos sustituciones de arriba.
- ❌ Una base compartida entre escenarios con limpieza manual — base nueva por escenario, siempre.
- ❌ Assertear contra la base de datos lo que la API ya expone — el punto es verificar el contrato HTTP.
- ❌ `Data Source=:memory:` pelado con `IDbContextFactory` — cada contexto nuevo vería una base VACÍA distinta;
  tiene que ser named + `Cache=Shared` + keep-alive.
- ❌ Duplicar en E2E las reglas ya cubiertas por los 150+ tests de dominio/integración — acá van los flujos
  cross-aggregate y el contrato HTTP, no cada guard.
