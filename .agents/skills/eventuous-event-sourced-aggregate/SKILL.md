---
name: eventuous-event-sourced-aggregate
description: Crear o modificar un aggregate event-sourced con Eventuous (.NET) — eventos versionados, State inmutable, comandos y CommandService con handlers estáticos. Usar cuando una entidad de dominio tiene historial/transiciones de estado auditables (propone → aprueba → aplica/rechaza), no para read-models simples.
---

# eventuous-event-sourced-aggregate

Patrón para modelar una entidad de dominio como **event-sourced aggregate** con la librería
[Eventuous](https://eventuous.dev/) sobre .NET, con el idiom exacto: cuatro bloques en un solo archivo (eventos,
state, comandos, command service), handlers **estáticos** con `yield return`, y guards que tiran `DomainException`.

## Cuándo usar / cuándo no

- **Usar**: la entidad tiene un ciclo de vida con transiciones que hay que poder auditar/reproducir (ej. una
  tarea que pasa `Pending → InProgress → Done/Cancelled`, un pedido que se aprueba, un documento con versiones).
  El event store (Postgres vía Eventuous) es la fuente de verdad; nunca se hace `UPDATE` directo sobre el estado.
- **No usar**: para datos que solo se leen (eso es un read-model EF, ver [[ef-core-postgres-context]] y
  [[eventuous-projection-readmodel]]), ni para estado efímero/cache que no necesita historial.

## Estructura del archivo — CUATRO bloques en este orden

Un solo archivo `Domain/<Area>/<Aggregate>.cs` (ej. `Domain/Tasks/TaskItem.cs`):

### 1. Eventos — versionados desde el día uno

```csharp
using Eventuous;

namespace GastNyahp.Domain.Tasks;

public static class TaskItemEvents
{
    public static class V1
    {
        [EventType("V1.TaskCreated")]
        public record TaskCreated(string TaskId, string Title, string CreatedBy, string CreatedAt);

        [EventType("V1.TaskStarted")]
        public record TaskStarted(string TaskId, string StartedAt);

        [EventType("V1.TaskCompleted")]
        public record TaskCompleted(string TaskId, string CompletedAt, string? Note = null);

        [EventType("V1.TaskCancelled")]
        public record TaskCancelled(string TaskId, string Reason, string CancelledAt);
    }
}
```

Reglas duras:
- Namespace `V1` desde el primer evento — cuando cambie el shape, se agrega `V2` (nunca se edita un evento
  publicado; los handlers viejos siguen funcionando sobre streams viejos).
- `[EventType("V1.<Nombre>")]` en CADA evento — sin este atributo el tipo no se serializa/registra.
- Campos primitivos (string/int/bool). Las fechas van como `string` ISO-8601 (`DateTime.UtcNow.ToString("O")`),
  nunca `DateTime` crudo — evita ambigüedad de time zone/formato en el JSON persistido.

### 2. State — record inmutable, un `On<TEvento>` por transición

```csharp
public enum TaskStatus { Pending, InProgress, Done, Cancelled }

public record TaskItemState : State<TaskItemState>
{
    public string TaskId { get; init; } = "";
    public string Title { get; init; } = "";
    public TaskStatus Status { get; init; } = TaskStatus.Pending;
    public string CreatedAt { get; init; } = "";

    public TaskItemState()
    {
        On<TaskItemEvents.V1.TaskCreated>((s, e) => s with
        {
            TaskId = e.TaskId, Title = e.Title, Status = TaskStatus.Pending, CreatedAt = e.CreatedAt
        });
        On<TaskItemEvents.V1.TaskStarted>((s, _) => s with { Status = TaskStatus.InProgress });
        On<TaskItemEvents.V1.TaskCompleted>((s, _) => s with { Status = TaskStatus.Done });
        On<TaskItemEvents.V1.TaskCancelled>((s, _) => s with { Status = TaskStatus.Cancelled });
    }
}
```

El State se reconstruye replayando eventos — nunca se muta ni se persiste directamente. `s with { ... }` es la
ÚNICA forma de "cambiar" el estado.

### 3. Comandos — un record por operación

```csharp
public record CreateTask(string TaskId, string Title, string CreatedBy);
public record StartTask(string TaskId);
public record CompleteTask(string TaskId, string? Note = null);
public record CancelTask(string TaskId, string Reason);
```

### 4. CommandService — handlers ESTÁTICOS con `yield return`

```csharp
public sealed class TaskItemCommandService : CommandService<TaskItemState>
{
    public TaskItemCommandService(IEventStore store) : base(store)
    {
        On<CreateTask>().InState(ExpectedState.New)
            .GetStream(cmd => Stream(cmd.TaskId)).Act(Create);
        On<StartTask>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.TaskId)).Act(Start);
        On<CompleteTask>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.TaskId)).Act(Complete);
        On<CancelTask>().InState(ExpectedState.Existing)
            .GetStream(cmd => Stream(cmd.TaskId)).Act(Cancel);
    }

    // Comando sin estado previo: firma (Cmd) → IEnumerable<object>
    static IEnumerable<object> Create(CreateTask cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.TaskId)) throw new DomainException("CreateTask: TaskId required.");
        if (string.IsNullOrWhiteSpace(cmd.Title)) throw new DomainException("CreateTask: Title required.");
        yield return new TaskItemEvents.V1.TaskCreated(cmd.TaskId, cmd.Title, cmd.CreatedBy, Now);
    }

    // Comando que valida contra el estado actual: firma (State, object[] pastEvents, Cmd)
    static IEnumerable<object> Start(TaskItemState state, object[] _, StartTask cmd)
    {
        if (state.Status != TaskStatus.Pending)
            throw new DomainException($"StartTask: only Pending can start (was {state.Status}).");
        yield return new TaskItemEvents.V1.TaskStarted(cmd.TaskId, Now);
    }

    static IEnumerable<object> Complete(TaskItemState state, object[] _, CompleteTask cmd)
    {
        if (state.Status != TaskStatus.InProgress)
            throw new DomainException($"CompleteTask: only InProgress can complete (was {state.Status}).");
        yield return new TaskItemEvents.V1.TaskCompleted(cmd.TaskId, Now, cmd.Note);
    }

    static IEnumerable<object> Cancel(TaskItemState state, object[] _, CancelTask cmd)
    {
        if (state.Status is TaskStatus.Done or TaskStatus.Cancelled)
            throw new DomainException($"CancelTask: cannot cancel a {state.Status} task.");
        yield return new TaskItemEvents.V1.TaskCancelled(cmd.TaskId, cmd.Reason, Now);
    }

    static StreamName Stream(string id) => new($"task-{id}");
    static string Now => DateTime.UtcNow.ToString("O");
}
```

## Por qué handlers estáticos con `yield` (y no lambdas inline)

- Los handlers son funciones puras: `(cmd) → eventos` o `(state, cmd) → eventos`. No dependen de campos de
  instancia, así que testearlos es `Handler(cmd)` directo, sin mockear nada.
  Una lambda capturando `this`/campos de instancia tiende a esconder side-effects y hace más difícil razonar
  sobre qué datos usa el handler para decidir qué evento emitir.
- `yield return` permite emitir 0, 1 o N eventos desde el mismo comando sin armar una `List<object>` a mano.
- Guards con `throw new DomainException("...")`: mensajes explícitos que dicen qué comando falló y por qué
  (útil en logs y en el `result.Success == false` que devuelve `CommandService.Handle`).

## Procedimiento

1. Definí el archivo con los cuatro bloques (eventos → state → comandos → command service).
2. Escribí los guards ANTES del `yield return`: ids no vacíos, transición de estado válida, invariantes del
   dominio. Un guard = una línea `if (...) throw new DomainException("<Comando>: <por qué>.")`.
3. Registrá el `CommandService` en el DI del proyecto de infraestructura (ver [[csharp-conventions-and-patterns]]
   para la convención de `AddXxxServices(this IServiceCollection)`), como `AddSingleton<TaskItemCommandService>()`.
4. Si necesitás leer el estado desde afuera (HTTP, otro comando), NO expongas el aggregate directo: agregá un
   read-model vía proyección (ver [[eventuous-projection-readmodel]]) y un service de aplicación (ver
   [[application-service-layer]]) que orqueste comando + lectura.

## Verificación

- Build limpio.
- Un test de aggregate: aplicar comando(s) sobre el `CommandService` y assertar el `State` resultante o que
  `DomainException` se dispara en el guard esperado (sin tocar Postgres — usar un `InMemoryEventStore` si el
  proyecto lo tiene configurado para tests).
- Los eventos con `[EventType]` nuevos deben aparecer registrados (Eventuous los descubre por reflexión al
  bootear el `TypeMapper`/`AddEventuousPostgres`, según cómo esté armado el registro en el proyecto).

## Anti-patrones

- ❌ Lambdas inline en el `CommandService` capturando estado de instancia — separalas en un método estático.
- ❌ Olvidar `[EventType("V1.…")]` en un evento nuevo.
- ❌ Mutar el `State` — es un record inmutable, siempre `s with { ... }`.
- ❌ Lógica de negocio en el `State` (los `On<T>` solo proyectan campos) — la validación va en el
  `CommandService`, con guards que tiran `DomainException`.
- ❌ Reusar el mismo `record` de evento para dos significados distintos — un evento = un hecho de dominio con
  nombre inequívoco.
- ❌ Editar un evento `V1` ya publicado (agregar/quitar campos) en vez de crear `V2` — rompe el replay de
  streams existentes.
