using Eventuous.Subscriptions.Context;
using GastNyahp.Infrastructure.Projections;

namespace GastNyahp.Infrastructure.EventStore;

/// <summary>
/// How application services guarantee read-your-writes: after a successful command they call CatchUp and the
/// read model reflects the new events before the HTTP response leaves. In InMemory event-store mode this pump
/// IS the subscription (there is no background $all consumer); in the future Postgres mode a real Eventuous
/// subscription takes that role and this interface gets a lighter implementation.
/// </summary>
public interface IReadModelSync
{
    Task CatchUp(CancellationToken ct = default);
}

/// <summary>Feeds every not-yet-projected event of the store's global log to ALL projections, in append
/// order — the same semantics as the production $all subscription, minus the background thread.</summary>
public sealed class InMemoryProjectionPump(InMemoryEventStore store, IEnumerable<GastNyahpProjection> projections) : IReadModelSync
{
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly List<GastNyahpProjection> _projections = [.. projections];
    int _projectedUpTo;

    public async Task CatchUp(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var log = store.GlobalLog;
            for (; _projectedUpTo < log.Count; _projectedUpTo++)
            {
                var (stream, evt) = log[_projectedUpTo];
                var ctx = new MessageConsumeContext(
                    evt.Id.ToString(), evt.Payload!.GetType().Name, "application/json", stream.ToString(),
                    (ulong)evt.Revision, (ulong)evt.Revision, (ulong)_projectedUpTo, (ulong)_projectedUpTo,
                    DateTime.UtcNow, evt.Payload, evt.Metadata, "inmemory-pump", ct);

                foreach (var projection in _projections)
                    await projection.HandleEvent(ctx);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Rewinds the checkpoint to zero — the next CatchUp replays the whole log (read-model rebuild).</summary>
    public void Rewind() => _projectedUpTo = 0;
}
