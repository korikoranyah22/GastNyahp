---
name: mcp-tool-server
description: Exponer las funcionalidades de la app (no un LLM ni un agente) como tools de un servidor MCP en .NET, usando el SDK oficial ModelContextProtocol — para que clientes de IA (Codex.ai, Codex Desktop, Cursor) puedan invocar la app. Usar cuando se pide dar acceso a la app vía MCP; no aplica a nada relacionado con LLMs propios, agentes o embeddings.
---

# mcp-tool-server

Un servidor MCP en .NET que expone operaciones de la app (CRUD, consultas) como *tools* invocables por un
cliente de IA externo (Codex.ai, Codex Desktop, Cursor, etc.). Esto NO es "conectar la app a un LLM" — es lo
opuesto: la app se vuelve una *herramienta* que un LLM ajeno (el del cliente MCP) puede llamar. El servidor no
sabe nada de modelos, prompts, ni agentes.

## Cuándo usar / cuándo no

- **Usar**: querés que un cliente de IA pueda leer/escribir datos de la app (ej. "creame una tarea", "listame
  las tareas pendientes") desde fuera, invocando tools con parámetros tipados.
- **No usar**: para lógica de agentes/orquestación de LLM propia de la app — este servidor solo expone
  funcionalidad existente, delegando toda la lógica de negocio al [[application-service-layer]] ya escrito.

## Piezas — reusa el mismo backend, expone un frontend distinto

El servidor MCP es otro **proceso ASP.NET** (puede vivir en el mismo `docker-compose`, ver
[[docker-compose-service-network]]) que reutiliza los mismos services de aplicación
([[application-service-layer]]) y, opcionalmente, su propia base de datos/schema si necesita estado propio
(ej. tokens de auth). Puede compartir la instancia de Postgres del stack principal con un schema separado, o
tener la suya — cualquiera de las dos es válida; documentá cuál elegiste.

### 1. Definir las tools — `[McpServerToolType]` + `[McpServerTool]`

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace GastNyahp.McpServer.Tools;

[McpServerToolType]
public sealed class TaskTool
{
    [McpServerTool(Name = "task_list")]
    [Description("Lista las tareas. Si se especifica 'status', filtra por ese estado (Pending/InProgress/Done/Cancelled).")]
    public static async Task<string> ListTasks(
        TaskService tasks,
        [Description("Filtro opcional de estado.")] string? status = null)
    {
        var all = await tasks.GetAllAsync();
        var filtered = status is null ? all : all.Where(t => t.Status == status).ToList();
        if (filtered.Count == 0) return "(No hay tareas.)";
        return string.Join("\n", filtered.Select(t => $"- [{t.Status}] {t.Title} ({t.Id})"));
    }

    [McpServerTool(Name = "task_create")]
    [Description("Crea una tarea nueva.")]
    public static async Task<string> CreateTask(
        TaskService tasks,
        [Description("Título de la tarea.")] string title,
        [Description("Quién la crea.")] string createdBy)
    {
        var id = Guid.NewGuid().ToString("N");
        var ok = await tasks.CreateAsync(id, title, createdBy);
        return ok ? $"Tarea creada: {id}" : "Error: no se pudo crear la tarea.";
    }

    [McpServerTool(Name = "task_complete")]
    [Description("Marca una tarea como completada.")]
    public static async Task<string> CompleteTask(
        TaskService tasks,
        [Description("Id de la tarea.")] string task_id,
        [Description("Nota opcional.")] string? note = null)
    {
        var ok = await tasks.CompleteAsync(task_id, note);
        return ok ? "Tarea completada." : "Error: la tarea no puede completarse en su estado actual.";
    }
}
```

Reglas:
- Cada método estático es una tool. El SDK inyecta por parámetro cualquier servicio registrado en DI (acá,
  `TaskService` directo — el mismo de [[application-service-layer]], sin duplicar lógica).
- `[Description]` en la tool Y en cada parámetro — es lo que el modelo del lado cliente lee para decidir cómo
  y cuándo llamarla. Sin descripciones claras, el cliente de IA adivina mal los argumentos.
- Devolvé **texto plano legible**, no JSON crudo — el resultado se lo lee un LLM del otro lado, no un
  parser. Formateá listas/errores como texto humano.
- Nombres de tool con snake_case y prefijo del dominio (`task_list`, `task_create`) — evita colisiones si más
  adelante se agregan tools de otro módulo.

### 2. Registro en `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGastNyahpInfrastructure(builder.Configuration); // el mismo AddXxx de csharp-conventions-and-patterns

builder.Services
    .AddMcpServer()
    .WithToolsFromAssembly(typeof(TaskTool).Assembly)
    .WithHttpTransport();

var app = builder.Build();

app.MapGet("/health", () => Results.Json(new { status = "ok" }));

app.MapMcp("/mcp"); // el endpoint MCP queda en POST/GET /mcp
app.Run();
```

`WithToolsFromAssembly` descubre por reflexión todas las clases `[McpServerToolType]` del assembly indicado —
si las tools viven en un proyecto separado (recomendado, para poder compartirlas entre transporte HTTP y
transporte Stdio sin duplicar código), pasá el assembly de ESE proyecto.

### 3. Auth — bearer token simple (suficiente para una app propia)

```csharp
public sealed class BearerAuthMiddleware(RequestDelegate next, ITokenValidator tokens)
{
    static readonly string[] ExemptPrefixes = { "/health" };

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ExemptPrefixes.Any(p => ctx.Request.Path.StartsWithSegments(p)))
        {
            await next(ctx);
            return;
        }

        var header = ctx.Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ") ? header["Bearer ".Length..].Trim() : null;

        if (token is null || !await tokens.ValidateAsync(token, ctx.RequestAborted))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { error = "unauthorized" });
            return;
        }

        await next(ctx);
    }
}
```

```csharp
app.UseMiddleware<BearerAuthMiddleware>();
app.MapMcp("/mcp");
```

Un bearer token estático (emitido por un endpoint de admin propio, o simplemente una env var compartida) es
suficiente para uso personal/interno. Si el cliente MCP requiere un login interactivo (Codex.ai en modo
"remote MCP connector" pide un flujo OAuth2 completo con PKCE), esa es una capa aparte que se agrega SOLO si
el cliente concreto lo exige — no la implementes de entrada si el consumidor sos vos mismo con un token fijo.

## Procedimiento

1. Identificá qué operaciones del [[application-service-layer]] ya existente querés exponer.
2. Escribí una clase `[McpServerToolType]` con un método estático `[McpServerTool]` por operación, devolviendo
   texto legible.
3. Registrá `AddMcpServer().WithToolsFromAssembly(...).WithHttpTransport()` y `MapMcp("/mcp")`.
4. Agregá bearer auth si el servidor va a estar expuesto más allá de `localhost`.
5. Si el stack ya tiene un `docker-compose.yml` (ver [[docker-compose-service-network]]), sumá el servicio ahí:
   build propio, healthcheck en `/health`, `depends_on` de la base de datos si comparte schema.

## Verificación

- Levantar el server y confirmar `/health` responde.
- Conectar un cliente MCP (Codex Desktop, o `npx @modelcontextprotocol/inspector http://localhost:<puerto>/mcp`)
  y confirmar que las tools aparecen listadas con sus descripciones y que invocarlas devuelve el resultado
  esperado (probar tanto el camino feliz como un guard de dominio que debería fallar controladamente).

## Anti-patrones

- ❌ Meter lógica de negocio nueva en la tool en vez de delegar al service de aplicación existente — la tool es
  SOLO una traducción de "invocación MCP" a "llamada al service", igual que un controller HTTP.
- ❌ Devolver JSON crudo sin formatear como respuesta de la tool — el consumidor es un modelo de lenguaje, no un
  parser; dale texto legible.
- ❌ `[Description]` vacías o genéricas — el cliente de IA elige mal la tool/los argumentos sin buen contexto.
- ❌ Confundir esto con "agregarle un LLM a la app" — este servidor no llama a ningún modelo, solo expone datos
  y operaciones existentes.
- ❌ Implementar el flujo OAuth2 PKCE completo por defecto cuando un bearer token estático alcanza para el caso
  de uso real.
