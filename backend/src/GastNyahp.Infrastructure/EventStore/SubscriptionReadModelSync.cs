using Npgsql;

namespace GastNyahp.Infrastructure.EventStore;

/// <summary>
/// Postgres-mode read-your-writes: after a successful command, wait until the $all subscription's checkpoint
/// reaches the event log's head as of NOW (later appends don't extend the wait). This keeps the same contract
/// the InMemory pump gives tests — when a write returns, the read model already reflects it — which the
/// frontend relies on (it re-fetches lists right after each mutation) and FamilyService relies on across
/// streams (issue admin invite → validate it from the read model). Uses its own plain data source: it only
/// reads scalar bigints, so it can't trip over the eventuous.stream_message composite type. On timeout it
/// degrades to eventual consistency instead of failing the request.
/// </summary>
public sealed class SubscriptionReadModelSync(string connectionString, string schema, string subscriptionId)
    : IReadModelSync, IDisposable
{
    static readonly TimeSpan CatchUpTimeout = TimeSpan.FromSeconds(10);
    static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(25);

    readonly NpgsqlDataSource _dataSource = NpgsqlDataSource.Create(connectionString);

    public async Task CatchUp(CancellationToken ct = default)
    {
        var head = await ReadHead(ct);
        if (head is null) return;

        var deadline = DateTime.UtcNow + CatchUpTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await ReadCheckpoint(ct) >= head) return;
            await Task.Delay(PollDelay, ct);
        }
    }

    async Task<long?> ReadHead(CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand($"select max(global_position) from {schema}.messages");
        return await cmd.ExecuteScalarAsync(ct) as long?;
    }

    async Task<long?> ReadCheckpoint(CancellationToken ct)
    {
        await using var cmd = _dataSource.CreateCommand($"select position from {schema}.checkpoints where id = $1");
        cmd.Parameters.AddWithValue(subscriptionId);
        return await cmd.ExecuteScalarAsync(ct) as long?;
    }

    public void Dispose() => _dataSource.Dispose();
}
