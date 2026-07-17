using System.Runtime.CompilerServices;
using Eventuous;

namespace GastNyahp.Infrastructure.EventStore;

/// <summary>
/// Faithful in-memory IEventStore for integration tests (Eventuous 0.16.4 no longer ships one): per-stream
/// optimistic concurrency (NoStream / exact version / Any), StreamNotFound on missing reads, and a global
/// append-ordered log so the test host can simulate the $all subscription that feeds projections.
/// </summary>
public sealed class InMemoryEventStore : IEventStore
{
    readonly Lock _lock = new();
    readonly Dictionary<StreamName, List<StreamEvent>> _streams = [];
    readonly List<(StreamName Stream, StreamEvent Event)> _global = [];

    public IReadOnlyList<(StreamName Stream, StreamEvent Event)> GlobalLog
    {
        get { lock (_lock) return [.. _global]; }
    }

    public Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken)
    {
        lock (_lock) return Task.FromResult(_streams.TryGetValue(stream, out var list) && list.Count > 0);
    }

    public Task<AppendEventsResult> AppendEvents(
        StreamName stream, ExpectedStreamVersion expectedVersion, IReadOnlyCollection<NewStreamEvent> events, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var exists = _streams.TryGetValue(stream, out var list) && list!.Count > 0;

            if (expectedVersion == ExpectedStreamVersion.NoStream && exists)
                throw new OptimisticConcurrencyException(stream, new InvalidOperationException($"Stream '{stream}' already exists."));
            if (expectedVersion.Value >= 0 && (!exists || list!.Count - 1 != expectedVersion.Value))
                throw new OptimisticConcurrencyException(stream, new InvalidOperationException(
                    $"Stream '{stream}' is at version {(exists ? list!.Count - 1 : -1)}, expected {expectedVersion.Value}."));

            if (list is null) _streams[stream] = list = [];

            foreach (var e in events)
            {
                var stored = new StreamEvent(e.Id, e.Payload, e.Metadata ?? new Metadata(), "application/json", list.Count, DateTime.UtcNow, false);
                list.Add(stored);
                _global.Add((stream, stored));
            }

            return Task.FromResult(new AppendEventsResult((ulong)_global.Count, list.Count - 1));
        }
    }

    public Task<AppendEventsResult[]> AppendEvents(IReadOnlyCollection<NewStreamAppend> appends, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Batch multi-stream appends are not used by the aggregate command services under test.");

    public async IAsyncEnumerable<StreamEvent> ReadEvents(
        StreamName stream, StreamReadPosition start, int count, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        StreamEvent[] snapshot;
        lock (_lock)
        {
            if (!_streams.TryGetValue(stream, out var list) || list.Count == 0)
                throw new StreamNotFound(stream);
            snapshot = list.Skip((int)start.Value).Take(count).ToArray();
        }
        foreach (var e in snapshot)
        {
            await Task.Yield();
            yield return e;
        }
    }

    public async IAsyncEnumerable<StreamEvent> ReadEventsBackwards(
        StreamName stream, StreamReadPosition start, int count, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        StreamEvent[] snapshot;
        lock (_lock)
        {
            if (!_streams.TryGetValue(stream, out var list) || list.Count == 0)
                throw new StreamNotFound(stream);
            snapshot = list.AsEnumerable().Reverse().Take(count).ToArray();
        }
        foreach (var e in snapshot)
        {
            await Task.Yield();
            yield return e;
        }
    }

    public Task TruncateStream(StreamName stream, StreamTruncatePosition truncatePosition, ExpectedStreamVersion expectedVersion, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Stream truncation is not used by GastNyahp.");

    public Task DeleteStream(StreamName stream, ExpectedStreamVersion expectedVersion, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Stream deletion is not used by GastNyahp (removal is an event, not a stream delete).");
}
