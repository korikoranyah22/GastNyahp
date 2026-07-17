---
name: eventuous-projection-readmodel
description: Crear una proyección (read-model) que escucha eventos de un aggregate Eventuous y los pliega en una tabla EF Core/Postgres. Usar cuando necesitás consultar/listar/filtrar datos de un aggregate desde HTTP o desde otro servicio, sin reconstruir el stream cada vez.
---

# eventuous-projection-readmodel

Patrón CQRS: el aggregate (ver [[eventuous-event-sourced-aggregate]]) es la escritura; una **proyección**
escucha sus eventos vía subscription de Eventuous y mantiene una tabla EF Core plana, optimizada para leer
(filtros, joins, paginado). El read-model es descartable y reconstruible: si se corrompe, se puede borrar y
re-proyectar desde el event store.

## Cuándo usar / cuándo no

- **Usar**: necesitás listar/buscar/paginar instancias de un aggregate, o exponer sus datos en un endpoint REST.
- **No usar**: si solo necesitás el estado de UNA instancia puntual para decidir una transición (eso ya lo da el
  `CommandService.Handle` al cargar el `State` desde el stream) — ver [[eventuous-event-sourced-aggregate]].

## Piezas

### 1. Entidad EF (tabla plana, sin lógica)

```csharp
using System.ComponentModel.DataAnnotations;

namespace GastNyahp.Infrastructure.Projections;

public class TaskItemEntity
{
    [Key, MaxLength(64)]
    public string Id { get; set; } = "";
    [MaxLength(200)]
    public string Title { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### 2. El projection handler — extiende `EventHandler`, un `On<TEvento>` por evento

```csharp
using Eventuous.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using EventHandler = Eventuous.Subscriptions.EventHandler;

namespace GastNyahp.Infrastructure.Projections;

public class TaskItemProjection : EventHandler
{
    readonly IDbContextFactory<ProjectionsDbContext> _dbFactory;

    public TaskItemProjection(IDbContextFactory<ProjectionsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        On<TaskItemEvents.V1.TaskCreated>(ctx => new ValueTask(HandleAsync(ctx.Message, ctx.CancellationToken)));
        On<TaskItemEvents.V1.TaskStarted>(ctx => new ValueTask(OnStatusChange(ctx.Message.TaskId, "InProgress", ctx.CancellationToken)));
        On<TaskItemEvents.V1.TaskCompleted>(ctx => new ValueTask(OnStatusChange(ctx.Message.TaskId, "Done", ctx.CancellationToken)));
        On<TaskItemEvents.V1.TaskCancelled>(ctx => new ValueTask(OnStatusChange(ctx.Message.TaskId, "Cancelled", ctx.CancellationToken)));
    }

    public async Task HandleAsync(TaskItemEvents.V1.TaskCreated e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Idempotencia (ver sección de abajo): pre-check + swallow del unique_violation.
        if (await db.Tasks.AnyAsync(t => t.Id == e.TaskId, ct)) return;

        db.Tasks.Add(new TaskItemEntity
        {
            Id = e.TaskId, Title = e.Title, Status = "Pending",
            CreatedBy = e.CreatedBy, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505")
        {
            // Insertado concurrentemente (ver por qué en la sección "Idempotencia obligatoria").
        }
    }

    async Task OnStatusChange(string taskId, string status, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (entity is null) return; // el create todavía no llegó / ya se re-proyectó — no es un error
        entity.Status = status;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
```

### 3. Registro: DbSet + suscripción al `$all` stream

- Agregá `DbSet<TaskItemEntity> Tasks` al `ProjectionsDbContext` (ver [[ef-core-postgres-context]]).
- Registrá `TaskItemProjection` como parte de la suscripción "all-stream" del proyecto (Eventuous suele tener
  UNA subscription que agrupa todos los `EventHandler` — buscá cómo está armada
  `AddEventuousSubscription`/`AddSubscription` en la infraestructura del proyecto y sumá el handler ahí).

## Idempotencia obligatoria — por qué

Un mismo evento puede procesarse dos veces:
1. La app hace un **camino síncrono**: tras `commandService.Handle(...)`, el service de aplicación
   (ver [[application-service-layer]]) llama la proyección DIRECTO para que el caller vea el dato ya listo
   ("read-your-writes"), sin esperar a que la subscription lo levante del `$all`.
2. La **subscription** después replaya el mismo evento desde el stream.

Si el handler no es idempotente, el segundo insert tira `23505 unique_violation` y — peor — Eventuous NO avanza
el checkpoint de esa subscription, así que TODOS los handlers que comparten esa suscripción (porque leen del
mismo `$all`) quedan trabados en esa posición para siempre. Por eso: `AnyAsync` pre-check + `catch` del
`23505` específico, nunca dejar que la excepción se propague sin capturarla.

## Procedimiento

1. Creá la entidad EF (`Projections/<Aggregate>Entity.cs`) — sin lógica, solo props + `[MaxLength]`.
2. Creá el projection handler extendiendo `EventHandler`, un `On<T>` + método async por evento.
3. En el handler de creación (o cualquiera que haga `INSERT`), aplicá el patrón idempotente de arriba.
4. Agregá el `DbSet` y generá la migración EF (ver [[ef-core-postgres-context]]).
5. Registrá el handler en la subscription del proyecto.

## Verificación

- Build limpio + migración aplicada.
- Aplicar un comando end-to-end (vía el service de aplicación) y verificar que la fila aparece en la tabla.
- Si el proyecto tiene forma de "replay" (borrar la tabla + rebobinar el checkpoint), correrla una vez y
  confirmar que el read-model se reconstruye idéntico.

## Anti-patrones

- ❌ Handler de creación sin idempotencia — el primer 23505 sin capturar traba TODA la subscription compartida.
- ❌ Poner lógica de negocio/validación en la proyección — eso ya pasó en el `CommandService` (aggregate). La
  proyección solo pliega hechos ya validados.
- ❌ Un `DbContext` por request compartido entre proyección y controller — usá `IDbContextFactory` y
  `await using` por operación (ver [[ef-core-postgres-context]]).
- ❌ Asumir orden estricto entre proyecciones distintas del mismo evento — cada handler debe ser independiente.
