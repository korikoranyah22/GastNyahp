---
name: application-service-layer
description: Escribir el service de aplicación que orquesta comandos Eventuous (escritura) y lecturas EF Core (read-model) detrás de una API única. Usar cuando un controller necesita crear/actualizar y también listar/consultar la misma entidad, sin exponer el aggregate ni el DbContext directo.
---

# application-service-layer

La capa que vive ENTRE el controller HTTP (ver [[aspnet-rest-endpoint]]) y las dos mitades de CQRS: el
`CommandService` de Eventuous (escritura, ver [[eventuous-event-sourced-aggregate]]) y el `DbContext` del
read-model (lectura, ver [[eventuous-projection-readmodel]]). El controller nunca habla con Eventuous ni con EF
directamente — siempre pasa por este service.

## Cuándo usar / cuándo no

- **Usar**: cualquier entidad que tenga tanto comandos (crear/actualizar/transicionar) como consultas
  (listar/buscar/obtener por id) — es el punto de entrada único que un controller o una MCP tool invoca.
- **No usar**: para lógica que no toca el aggregate ni el read-model (ej. un helper de formateo puro) — eso no
  necesita DI ni ser "service" registrado.

## Patrón — primary constructor + result.Success + read-your-writes

```csharp
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Infrastructure.Services;

public class TaskService(
    TaskItemCommandService commandService,
    TaskItemProjection projection,
    IDbContextFactory<ProjectionsDbContext> dbFactory,
    ILogger<TaskService> logger)
{
    // ── Read ──────────────────────────────────────────────────────────────────

    public async Task<List<TaskItemEntity>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Tasks.OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
    }

    public async Task<TaskItemEntity?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(id)) return null;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    public async Task<bool> CreateAsync(string id, string title, string createdBy, CancellationToken ct = default)
    {
        var result = await commandService.Handle(new CreateTask(id, title, createdBy), ct);

        if (!result.Success)
        {
            logger.LogError(result.Exception, "[Task] CreateAsync failed for '{Id}'", id);
            return false;
        }

        // Read-your-writes: proyectamos DIRECTO acá, sin esperar a que la subscription
        // del $all levante el evento. La proyección es idempotente (ver
        // eventuous-projection-readmodel), así que el doble-procesamiento cuando la
        // subscription lo replaye después es inofensivo.
        await projection.HandleAsync(
            new TaskItemEvents.V1.TaskCreated(id, title, createdBy, DateTime.UtcNow.ToString("O")), ct);
        return true;
    }

    public async Task<bool> CompleteAsync(string id, string? note, CancellationToken ct = default)
    {
        var result = await commandService.Handle(new CompleteTask(id, note), ct);
        if (!result.Success)
        {
            logger.LogWarning(result.Exception, "[Task] CompleteAsync failed for '{Id}'", id);
            return false;
        }
        return true; // acá no re-proyectamos síncrono: la subscription alcanza en <1s y no hay
                     // un read inmediato después en el mismo request que lo necesite.
    }
}
```

## Reglas del patrón

1. **Un método = una intención de negocio**, no un CRUD genérico. `CreateAsync`, `CompleteAsync`,
   `CancelAsync` — no `ExecuteCommand(object cmd)`.
2. **Nunca dejes que `DomainException` se propague cruda hasta el controller** desde este service si el
   controller espera un booleano/resultado — revisá `result.Success` y logueá `result.Exception`. El controller
   decide el código HTTP (ver [[aspnet-rest-endpoint]]); este service decide si la operación de dominio anduvo.
3. **Read-your-writes es una decisión explícita, no un default**: solo re-proyectás síncrono cuando el caller
   necesita ver el dato inmediatamente después (ej. el `POST` que crea y el frontend espera el objeto creado en
   la respuesta). Si nadie lee inmediatamente después, dejá que la subscription lo proyecte async.
4. **Primary constructor** para las dependencias (`TaskService(Dep1 a, Dep2 b, ...)`) — ver
   [[csharp-conventions-and-patterns]].
5. El service se registra como servicio de aplicación (`AddScoped`/`AddSingleton` según si tiene estado — la
   mayoría de estos son singletons porque no guardan estado propio, solo delegan a `IDbContextFactory` y al
   `CommandService`).

## Procedimiento

1. Identificá el aggregate (comandos) y el read-model (proyección) que este service va a orquestar.
2. Escribí los métodos de lectura primero (son triviales: `DbContext` + LINQ).
3. Escribí los métodos de escritura: `commandService.Handle(cmd, ct)` → chequear `result.Success` → decidir si
   hace falta re-proyectar síncrono.
4. Registrá el service en el DI del proyecto de infraestructura.
5. El controller HTTP (o la MCP tool) llama SOLO a este service, nunca al `CommandService` ni al `DbContext`
   directamente.

## Verificación

- Build limpio.
- Un test que llama `CreateAsync` y después `GetByIdAsync` en el mismo test y encuentra el dato — valida el
  read-your-writes.
- Un test que fuerza un guard del aggregate (ej. `CompleteAsync` sobre una tarea `Pending`) y verifica que el
  service devuelve `false`/loguea, sin tirar una excepción sin manejar.

## Anti-patrones

- ❌ El controller inyectando `CommandService` o `DbContext` directo, saltándose este service.
- ❌ Un método "genérico" que recibe el comando como `object` — perdés el tipado y la intención queda oculta.
- ❌ Re-proyectar síncrono en TODOS los métodos "por las dudas" — es trabajo extra innecesario cuando nadie va a
  leer el dato en el mismo request.
- ❌ Tragarse la excepción sin loguearla — el `result.Exception` es la única pista de qué guard del dominio
  falló.
