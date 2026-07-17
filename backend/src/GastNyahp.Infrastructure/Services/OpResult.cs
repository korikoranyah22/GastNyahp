using Eventuous;
using GastNyahp.Infrastructure.EventStore;
using Microsoft.Extensions.Logging;

namespace GastNyahp.Infrastructure.Services;

/// <summary>
/// Outcome of an application-service write. Same philosophy as the frontend store's `{ error }` return shape:
/// domain failures are values the controller maps to 422, never exceptions crossing layers.
/// </summary>
public readonly record struct OpResult(bool Ok, string? Error, Guid? Id = null)
{
    public static OpResult Success(Guid? id = null) => new(true, null, id);
    public static OpResult Fail(string error) => new(false, error);
}

/// <summary>Shared write pipeline: run the command, surface the domain error, sync the read model
/// (read-your-writes) only on success.</summary>
internal static class CommandExecutor
{
    public static async Task<OpResult> Exec<TState>(
        Task<Result<TState>> handling, IReadModelSync sync, ILogger logger, string operation,
        Guid? id = null, CancellationToken ct = default)
        where TState : State<TState>, new()
    {
        var result = await handling;
        if (!result.Success)
        {
            logger.LogWarning(result.Exception, "[{Operation}] command rejected", operation);
            return OpResult.Fail(result.Exception?.Message ?? $"{operation}: operación rechazada.");
        }

        await sync.CatchUp(ct);
        return OpResult.Success(id);
    }
}
