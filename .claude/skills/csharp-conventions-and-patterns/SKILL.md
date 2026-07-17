---
name: csharp-conventions-and-patterns
description: Convenciones de C#/.NET del proyecto — registro de DI por módulo con extension methods, Options pattern para configuración, records inmutables, primary constructors, guard clauses con excepciones de dominio, e inicialización de infraestructura vía IHostedService. Usar como referencia general de estilo al escribir cualquier código de backend nuevo.
---

# csharp-conventions-and-patterns

Estilo transversal de C#/.NET para este stack. No es específico de un feature — aplicalo en cualquier clase
nueva de backend, junto con los patrones más puntuales de [[eventuous-event-sourced-aggregate]],
[[application-service-layer]] y [[ef-core-postgres-context]].

## 1. DI registrado por módulo, con un extension method por proyecto

Cada proyecto de la solución (`GastNyahp.Infrastructure`, `GastNyahp.Api`, etc.) expone UN método
`Add<Módulo>(this IServiceCollection services, IConfiguration configuration)` que registra todo lo suyo. El
`Program.cs` del host solo encadena llamadas a estos métodos — nunca registra servicios individuales sueltos
ahí.

```csharp
namespace GastNyahp.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGastNyahpInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TaskOptions>(configuration.GetSection("Tasks"));

        services.AddDbContextFactory<ProjectionsDbContext>(/* ... */);
        services.AddSingleton<TaskItemCommandService>();
        services.AddSingleton<TaskItemProjection>();
        services.AddSingleton<TaskService>();

        return services;
    }
}
```

En `Program.cs`:
```csharp
builder.Services.AddGastNyahpInfrastructure(builder.Configuration);
```

## 2. Options pattern — nunca leer `IConfiguration` a mano dentro de un service

```csharp
public class TaskOptions
{
    public int MaxOpenTasksPerUser { get; set; } = 50;
    public bool AutoCloseStaleTasks { get; set; } = false;
}
```

Registro: `services.Configure<TaskOptions>(configuration.GetSection("Tasks"));`
Consumo: inyectar `IOptions<TaskOptions>` (o `IOptionsMonitor<T>` si necesita hot-reload) — nunca
`configuration["Tasks:MaxOpenTasksPerUser"]` disperso por el código. Una feature opcional (gateway externo,
integración) se activa/desactiva revisando si su `ApiKey`/config requerida está vacía al momento del registro
en DI — si falta, simplemente no se registra el servicio, y el código que lo usaría hace early-return o cae a
un fallback, en vez de tirar una excepción de "servicio no configurado" en runtime.

## 3. Records inmutables + `with` para todo lo que representa un hecho o un estado

- Eventos de dominio, comandos, y el `State` de un aggregate: siempre `record`, nunca `class` mutable (ver
  [[eventuous-event-sourced-aggregate]]).
- DTOs de request/response HTTP: `record` también (ver [[aspnet-rest-endpoint]]) — igualdad estructural gratis,
  útil en tests.
- Entidades EF (`TaskItemEntity`): estas SÍ son `class` con setters, porque EF Core necesita poder
  materializarlas y trackear cambios — la inmutabilidad de records no aplica ahí.

## 4. Primary constructors para DI — no escribas el ctor a mano

```csharp
public class TaskService(
    TaskItemCommandService commandService,
    IDbContextFactory<ProjectionsDbContext> dbFactory,
    ILogger<TaskService> logger)
{
    // los parámetros del primary constructor ya son campos privados implícitos
}
```

Evitá el boilerplate de `private readonly X _x; public Ctor(X x) => _x = x;` salvo que necesites lógica extra
en la construcción (en ese caso, un ctor explícito sigue siendo válido).

## 5. Guard clauses con excepción de dominio propia, no `ArgumentException` genérica

Definí una excepción propia del dominio (ej. `DomainException`, o la que traiga Eventuous) y usala para TODA
validación de invariantes de negocio, con un mensaje que diga qué comando/operación falló y por qué:

```csharp
if (string.IsNullOrWhiteSpace(cmd.Title))
    throw new DomainException("CreateTask: Title required.");
```

Reservá `ArgumentNullException`/`ArgumentException` para violaciones de contrato de API pura (un parámetro
`null` que nunca debería llegar), no para reglas de negocio.

## 6. Inicialización de infraestructura vía `IHostedService`, no en `Program.cs` a pelo

Migraciones, seed de datos, warm-up de un cliente externo: envolvé la lógica en una clase que implementa
`IHostedService`/`BackgroundService` y registrala con `AddHostedService`. Así el orden de arranque queda
explícito y testeable, en vez de código suelto entre los `builder.Services.Add...` del `Program.cs`.

```csharp
public class ProjectionsDatabaseInitializer(IDbContextFactory<ProjectionsDbContext> dbFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

```csharp
services.AddSingleton<ProjectionsDatabaseInitializer>();
services.AddHostedService(sp => sp.GetRequiredService<ProjectionsDatabaseInitializer>());
```

Registrar el mismo singleton dos veces (una directa, una como `IHostedService`) permite que otras clases lo
inyecten directamente cuando necesiten forzar una operación (ej. un endpoint de admin para re-migrar) sin
duplicar la instancia.

## Anti-patrones

- ❌ `Program.cs` con decenas de `services.AddXxx()` sueltos en vez de un `AddGastNyahpXxx()` por proyecto.
- ❌ Leer `IConfiguration["Seccion:Clave"]` disperso en vez de bindear una clase de Options.
- ❌ Clases de dominio (`State`, eventos, comandos) mutables.
- ❌ Ctors escritos a mano cuando un primary constructor alcanza.
- ❌ `throw new Exception("algo salió mal")` genérico — siempre una excepción tipada con mensaje específico.
- ❌ Lógica de startup (migraciones, seed) pegada directo en `Program.cs` en vez de un `IHostedService`.
