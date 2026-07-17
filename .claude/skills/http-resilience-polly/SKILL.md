---
name: http-resilience-polly
description: Agregar una política de reintentos Polly a un HttpClient tipado saliente. Usar cada vez que el backend llama a un servicio externo o a otro contenedor de la red Docker por HTTP, para que blips transitorios de red no tumben el request.
---

# http-resilience-polly

Regla del proyecto: **todo `HttpClient` tipado que salga del backend tiene que tener una política de retry de
Polly con al menos 3 intentos.** Sin esto, cualquier hipo de red entre contenedores (o hacia un servicio
externo) se convierte en un error 500 visible para el usuario en vez de resolverse solo.

## Cuándo usar / cuándo no

- **Usar**: cualquier `HttpClient` nombrado/tipado que llame a otro servicio de la red Docker (ver
  [[docker-compose-service-network]]) o a una API externa.
- **No usar**: para llamadas in-process (no hay red de por medio) ni para operaciones donde un reintento
  automático sería incorrecto (ej. un webhook que ya fue aceptado por el otro lado pero la respuesta se perdió
  — ahí hay que pensar en idempotencia del lado receptor primero).

## Por qué un pipeline custom y no `AddStandardResilienceHandler`

El handler estándar de `Microsoft.Extensions.Http.Resilience` agrega un timeout total de ~30s al request
completo. Eso corta respuestas que legítimamente tardan más (streaming, un job que corre en el contenedor
llamado). El pipeline de este proyecto solo reintenta ante fallas transitorias y NO impone un techo de tiempo
total — dejá que el timeout, si hace falta, lo decida el caller vía `CancellationToken`.

## El helper reutilizable

```csharp
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace GastNyahp.Infrastructure.Resilience;

public static class HttpClientResilienceExtensions
{
    public static IHttpClientBuilder AddStandardRetry(this IHttpClientBuilder builder, string clientName)
    {
        builder.AddResilienceHandler($"{clientName}-retry", (pipeline, ctx) =>
        {
            var logger = ctx.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger($"HttpClient.{clientName}");

            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay            = TimeSpan.FromMilliseconds(200),
                BackoffType      = DelayBackoffType.Exponential,
                UseJitter        = true,
                ShouldHandle     = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(ex =>
                        // Sin CT explícito, o el HttpClient.Timeout interno saltó (falla
                        // transitoria genuina) → retry. Si el CT EXTERNO fue cancelado
                        // (el usuario abortó la operación) → no retry, sería spam.
                        ex.CancellationToken == default
                        || ex.InnerException is TimeoutException
                        || !ex.CancellationToken.IsCancellationRequested)
                    .Handle<IOException>()
                    .HandleResult(r => (int)r.StatusCode >= 500
                                     || r.StatusCode == HttpStatusCode.RequestTimeout
                                     || r.StatusCode == HttpStatusCode.TooManyRequests),
                OnRetry = args =>
                {
                    logger.LogWarning(args.Outcome.Exception,
                        "[HttpClient/{Client}] Retry {Attempt}/3 after {Delay} — status={Status}",
                        clientName, args.AttemptNumber + 1, args.RetryDelay,
                        args.Outcome.Result?.StatusCode.ToString() ?? "exception");
                    return ValueTask.CompletedTask;
                },
            });
        });
        return builder;
    }
}
```

## Uso al registrar el HttpClient

```csharp
services.AddHttpClient("tasks-webhook", client =>
{
    client.BaseAddress = new Uri(configuration["TasksWebhook:BaseUrl"]!);
}).AddStandardRetry("tasks-webhook");
```

Consumo vía `IHttpClientFactory.CreateClient("tasks-webhook")` — nunca `new HttpClient()` directo, así siempre
hereda el pipeline de resiliencia.

## Qué reintentar y qué no

- **Sí**: `HttpRequestException`, `IOException`, 5xx, 408, 429 — todos transitorios por definición.
- **No**: 4xx que no sea 408/429 (son errores del request, reintentar no los arregla), ni una cancelación
  genuina del `CancellationToken` externo (el caller ya decidió abortar).

## Procedimiento

1. Registrá el `HttpClient` nombrado/tipado con `AddHttpClient(...)`.
2. Encadená `.AddStandardRetry("<nombre>")`.
3. Si el cliente hace streaming largo (SSE, descarga grande), confirmá que NO estás usando
   `AddStandardResilienceHandler` (que impondría el timeout de 30s) — usá este pipeline custom.

## Verificación

- Apagar momentáneamente el contenedor destino (`docker compose stop <servicio>`) y confirmar en los logs que
  aparecen los `OnRetry` warnings antes de fallar (o que se recupera si lo levantás durante los reintentos).
- Build limpio.

## Anti-patrones

- ❌ Un `HttpClient` saliente sin ninguna política de retry.
- ❌ Usar `AddStandardResilienceHandler` para clientes de streaming/larga duración.
- ❌ Reintentar 4xx que no son 408/429 (contrato roto, no falla transitoria).
- ❌ Reintentar cuando el `CancellationToken` externo ya fue cancelado por el usuario.
- ❌ `new HttpClient()` manual en vez de `IHttpClientFactory` — pierde el pipeline configurado.
