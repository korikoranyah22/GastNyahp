using Eventuous;
using Eventuous.Postgresql;
using Eventuous.Subscriptions.Checkpoints;
using GastNyahp.Domain.Banks;
using GastNyahp.Infrastructure;
using GastNyahp.Infrastructure.EventStore;
using GastNyahp.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GastNyahp.Integration.Tests;

/// <summary>
/// Wiring smoke test for EventStore:Provider=Postgres: Npgsql connects lazily, so the whole DI graph must
/// build and resolve WITHOUT a live Postgres. Runtime behavior (streams, subscription, checkpoint) is verified
/// by `docker compose up` — this test guards against the graph silently breaking during refactors.
/// </summary>
public class PostgresModeWiringTests
{
    static ServiceProvider BuildPostgresModeProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Postgres",
                ["EventStore:Provider"] = "Postgres",
                ["ConnectionStrings:Projections"] = "Host=localhost;Port=5432;Database=gastnyahpdb;Username=gastnyahp;Password=dummy",
                ["BusinessDay:Enabled"] = "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGastNyahpInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Postgres_mode_resolves_the_full_graph_without_a_live_database()
    {
        using var provider = BuildPostgresModeProvider();

        Assert.IsType<PostgresStore>(provider.GetRequiredService<IEventStore>());
        Assert.NotNull(provider.GetRequiredService<ICheckpointStore>());
        Assert.IsType<SubscriptionReadModelSync>(provider.GetRequiredService<IReadModelSync>());

        // Command + app services build on top of the store without touching the network.
        Assert.NotNull(provider.GetRequiredService<BankCommandService>());
        Assert.NotNull(provider.GetRequiredService<BankService>());
        Assert.NotNull(provider.GetRequiredService<FamilyService>());
    }

    [Fact]
    public void Unknown_event_store_provider_fails_fast_at_startup()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EventStore:Provider"] = "Cassandra",
                ["ConnectionStrings:Projections"] = "Data Source=:memory:",
            })
            .Build();

        Assert.Throws<NotSupportedException>(() => new ServiceCollection().AddGastNyahpEventStore(configuration));
    }
}
