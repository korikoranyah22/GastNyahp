using Eventuous.Postgresql;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace GastNyahp.Infrastructure.EventStore;

/// <summary>
/// Creates the Eventuous schema on a THROWAWAY data source, before any other component opens the shared one.
/// Npgsql snapshots the database's type catalog on a data source's first physical connection: if the shared
/// source connects while eventuous.stream_message doesn't exist yet (first boot with initializeDatabase: true,
/// where the schema scripts themselves open that first connection), every append afterwards fails with
/// "type 'eventuous.stream_message' was not found in the current database info" until the process restarts.
/// Bootstrapping out-of-band removes that window — AddEventuousPostgres must get initializeDatabase: false,
/// and this hosted service must be registered before the subscription's.
/// </summary>
public sealed class EventStoreSchemaInitializer(string connectionString, string schema, ILogger<Schema> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await using var bootstrap = NpgsqlDataSource.Create(connectionString);
        await new Schema(schema).CreateSchema(bootstrap, logger, ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
