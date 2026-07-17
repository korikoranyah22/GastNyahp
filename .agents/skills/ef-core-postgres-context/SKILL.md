---
name: ef-core-postgres-context
description: Diseñar o modificar un DbContext de EF Core sobre PostgreSQL (Npgsql) y generar su migración — IDbContextFactory, retry-on-failure, design-time factory, y el patrón multi-contexto. Usar para cualquier cambio de schema del read-model (columna, tabla, índice), nunca para el event store de un aggregate.
---

# ef-core-postgres-context

Convenciones para el lado EF Core + Npgsql del stack, separado a propósito del event store de Eventuous
(ver [[eventuous-event-sourced-aggregate]] — ese es append-only y NO se toca con EF).

## Cuándo usar / cuándo no

- **Usar**: agregar/cambiar una columna, tabla, índice de un read-model (ver
  [[eventuous-projection-readmodel]]), o cualquier tabla que la app lee/escribe directo por EF (sin pasar por
  eventos — ej. configuración, tokens, catálogos).
- **No usar**: para el stream de eventos de un aggregate — eso lo maneja Eventuous internamente sobre su propio
  schema (`AddEventuousPostgres`), no se modela con `DbSet`.

## Patrón: un DbContext por dominio, no uno gigante

Cada área lógica tiene SU `DbContext`, con su propia cadena de migraciones (carpeta `Migrations/<Area>/`) y,
si hace falta aislar colisiones de nombre de tabla, su propio schema Postgres. Preferí esto a un único
`AppDbContext` con 40 `DbSet` — cada contexto se puede extraer a otro servicio/deploy sin arrastrar el resto.

```csharp
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Infrastructure.Projections;

public class ProjectionsDbContext(DbContextOptions<ProjectionsDbContext> options) : DbContext(options)
{
    public DbSet<TaskItemEntity> Tasks { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<TaskItemEntity>().ToTable("tasks")
            .HasIndex(e => e.Status);
    }
}
```

## Registro en DI — `IDbContextFactory` + retry-on-failure

Usar **siempre** `IDbContextFactory<T>`, no `AddDbContext` clásico — permite crear contextos de vida corta
dentro de un `EventHandler`/service singleton sin pelearse con el scope de DI de ASP.NET (ver
[[eventuous-projection-readmodel]] y [[application-service-layer]], que hacen `await using var db = await
dbFactory.CreateDbContextAsync(ct)` por operación).

```csharp
services.AddDbContextFactory<ProjectionsDbContext>(opts =>
    opts.UseNpgsql(projectionsConnectionString,
        npgsql => npgsql.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null)));
```

`EnableRetryOnFailure` es obligatorio en todo `UseNpgsql`: blips de red/reinicio de Postgres en Docker no deben
tirar abajo un request — reintenta la conexión antes de fallar. Sin esto, un simple `docker compose restart
postgres` durante desarrollo tira excepciones en cascada por toda la app.

## Design-time factory — para poder scaffoldear migraciones sin bootear la app entera

`dotnet ef migrations add` necesita construir el `DbContext` fuera del `Program.cs` normal. Si el contexto no
tiene un ctor sin parámetros resolvible, agregá una `IDesignTimeDbContextFactory<T>` que arme la connection
string desde variables de entorno (nunca hardcodeada):

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public class ProjectionsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ProjectionsDbContext>
{
    public ProjectionsDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Projections")
            ?? throw new InvalidOperationException(
                "Set ConnectionStrings__Projections (or POSTGRES_PASSWORD) before running `dotnet ef`.");
        var options = new DbContextOptionsBuilder<ProjectionsDbContext>().UseNpgsql(cs).Options;
        return new ProjectionsDbContext(options);
    }
}
```

## Generar la migración

```powershell
$env:POSTGRES_PASSWORD = 'dummy_for_migration_scaffold'
dotnet ef migrations add AddTasksTable `
  --project src/GastNyahp.Infrastructure `
  --startup-project src/GastNyahp.Api `
  --context ProjectionsDbContext `
  --output-dir Migrations/Projections
```

- `--context` SIEMPRE explícito si hay más de un `DbContext` en el proyecto — si no, EF adivina y puede tocar
  la cadena de migraciones equivocada.
- `--output-dir` separado por área (`Migrations/Projections`, `Migrations/CodeWorkspace`, etc.) así cada
  contexto tiene su propia carpeta de migraciones aislada.
- Después de generarla: leé el `Up()`/`Down()` generado y confirmá que hace SOLO tu cambio.

## Aplicar migraciones al arrancar (no `EnsureCreated` en prod)

```csharp
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ProjectionsDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}
```

`MigrateAsync` en un `IHostedService`/bloque de startup aplica migraciones pendientes contra Postgres al
bootear el container — así el schema del contenedor de base de datos siempre queda sincronizado con el código
desplegado, sin pasos manuales. `EnsureCreated()` es solo un fallback aceptable en un entorno de desarrollo sin
migraciones todavía (nunca en una base con datos reales, porque no aplica cambios incrementales).

## Procedimiento

1. Elegí el `DbContext` correcto para el cambio (o creá uno nuevo si es un dominio nuevo sin relación con los
   existentes).
2. Modificá la entidad + `OnModelCreating` (columnas, índices, longitudes con `[MaxLength]`).
3. Generá la migración con `--context` explícito.
4. Verificá el `Up()`/`Down()` generado.
5. Confirmá que el startup llama `MigrateAsync()` para ese contexto.

## Verificación

- Build limpio.
- La migración aparece en `Migrations/<Área>/` y el `Up()` refleja exactamente el cambio pedido.
- Levantar el container/app localmente y confirmar que la tabla/columna existe en Postgres
  (`docker compose exec postgres psql -U <user> -d <db> -c "\d tasks"`).

## Anti-patrones

- ❌ Un solo `DbContext` gigante para todo el sistema — dificulta extraer un dominio a otro servicio después.
- ❌ `AddDbContext` (scoped clásico) en vez de `AddDbContextFactory` cuando el consumidor es un singleton
  (proyección, background service) — termina con contextos dispuestos que siguen usándose.
- ❌ Omitir `EnableRetryOnFailure` — cualquier blip de Postgres se vuelve una excepción 500.
- ❌ Hardcodear la connection string con password en el design-time factory — siempre desde env vars.
- ❌ `EnsureCreated()` contra una base con datos reales en vez de migraciones incrementales.
- ❌ Columna `NOT NULL` sin default sobre una tabla que ya tiene filas — la migración falla al aplicar.
