---
name: aspnet-rest-endpoint
description: Agregar un endpoint REST en un controller de ASP.NET Core con las convenciones de DI por primary-constructor, manejo de errores por tipo de excepción, y records de request/response. Usar al exponer una operación de un service de aplicación por HTTP.
---

# aspnet-rest-endpoint

Convención de controller para exponer un [[application-service-layer]] por HTTP. El controller SOLO traduce
HTTP ↔ dominio — no contiene lógica de negocio.

## Cuándo usar / cuándo no

- **Usar**: exponer una operación de un service de aplicación (crear, listar, actualizar, transicionar) como
  ruta HTTP para el frontend (ver [[react-feature-module]]) o cualquier cliente REST.
- **No usar**: para lógica de negocio (eso vive en el service/aggregate) ni para exponerle la funcionalidad
  directo a un cliente de IA — para eso ver [[mcp-tool-server]].

## Patrón completo

```csharp
using Microsoft.AspNetCore.Mvc;

namespace GastNyahp.Api.Controllers;

[ApiController]
[Route("api/tasks")]
public sealed class TasksController(
    TaskService taskService,
    ILogger<TasksController> logger)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var tasks = await taskService.GetAllAsync(ct);
        return Ok(tasks);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var task = await taskService.GetByIdAsync(id, ct);
        return task is null ? NotFound() : Ok(task);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Title))
            return BadRequest("Title is required.");

        try
        {
            var ok = await taskService.CreateAsync(body.Id, body.Title, body.CreatedBy, ct);
            return ok ? Ok() : UnprocessableEntity("Could not create the task.");
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Tasks] Create failed for '{Id}'", body.Id);
            return StatusCode(500, "Unexpected error.");
        }
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> Complete(string id, [FromBody] CompleteTaskRequest? body, CancellationToken ct)
    {
        var ok = await taskService.CompleteAsync(id, body?.Note, ct);
        return ok ? Ok() : UnprocessableEntity("Task cannot be completed in its current state.");
    }
}

// ── Request/response records — SIEMPRE al final del archivo ─────────────────

public record CreateTaskRequest(string Id, string Title, string CreatedBy);
public record CompleteTaskRequest(string? Note);
```

## Reglas del patrón

1. **DI por primary constructor** — los services como parámetros de la clase, sin ctor explícito ni campos
   `private readonly` manuales.
2. **`CancellationToken ct` en toda acción async**, propagado a cada llamada async downstream. Nunca
   `CancellationToken.None` salvo que el trabajo deba sobrevivir a la desconexión del cliente (ver nota de
   fire-and-forget más abajo).
3. **Manejo de errores por capas**, en este orden:
   - Body inválido → `BadRequest(mensaje)` ANTES de llamar al service.
   - Excepción de dominio esperable (`InvalidOperationException` u otra específica que el service deje pasar)
     → `UnprocessableEntity(mensaje)`.
   - Cualquier otra excepción → `catch (Exception)`, loguear con el logger inyectado, `StatusCode(500, ...)`.
   No dejes una excepción cruda llegar al cliente sin capturar.
4. **Los `record` de request/response van al final del archivo**, no en un archivo `Dtos/` separado por
   endpoint — mantiene el contrato HTTP visible junto a la acción que lo usa.
5. **La ruta sigue el recurso** (`api/tasks`, `api/tasks/{id}/complete`) — verbos como sub-rutas solo para
   transiciones de estado que no son CRUD puro (`complete`, `cancel`, `approve`).
6. **camelCase en la respuesta JSON** lo da la configuración global de `System.Text.Json` (`PropertyNamingPolicy
   = JsonNamingPolicy.CamelCase`) en el `Program.cs` — no lo repitas por controller.

## Nota: operaciones largas (evitar 504 del proxy)

Un reverse-proxy (nginx, etc.) corta requests colgados a los ~30-1800s según config. Si una operación puede
tardar más que eso, NO la hagas síncrona en el request: devolvé `202 Accepted` con un id de tracking, arrancá
el trabajo en background (`IHostedService`/`Channel<T>`), y agregá un segundo endpoint `GET
/api/tasks/{id}/status` para que el cliente haga polling (o empujá el resultado por WebSocket/SignalR, ver
[[react-feature-module]]).

## Procedimiento

1. Identificá el service de aplicación que ya expone la operación (ver [[application-service-layer]]) — si no
   existe, escribilo primero ahí, no en el controller.
2. Agregá la acción al controller del recurso (o creá uno nuevo con el mismo molde).
3. Validá el body y propagá `ct`.
4. Definí los records de request/response al final del archivo.
5. Agregá un test de integración del endpoint (levantando la app en memoria contra una base de test).

## Verificación

- Build limpio + test del endpoint en verde.
- Probar manualmente con `curl`/Swagger que la ruta responde el shape esperado.
- El shape de la respuesta matchea lo que espera el cliente TypeScript (ver [[react-feature-module]]).

## Anti-patrones

- ❌ No validar el body / no propagar `ct`.
- ❌ Lógica de negocio en el controller (pertenece al service de aplicación).
- ❌ Dejar una excepción sin capturar (el cliente recibe un 500 con stack trace en vez de un mensaje útil).
- ❌ Un patrón de error distinto por controller — mantené SIEMPRE el mismo orden BadRequest →
  UnprocessableEntity → 500.
- ❌ Operaciones largas síncronas en el request — usar el patrón fire-and-forget + polling/push.
