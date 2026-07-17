using GastNyahp.Infrastructure.Projections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace GastNyahp.Infrastructure;

/// <summary>
/// Startup schema sync as an IHostedService (see csharp-conventions-and-patterns): Postgres applies pending
/// migrations; Sqlite (dev/E2E) uses EnsureCreated because the migration chain is Npgsql-flavored.
/// </summary>
public class ProjectionsDatabaseInitializer(IDbContextFactory<ProjectionsDbContext> dbFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
            await db.Database.EnsureCreatedAsync(ct);
        else
            await db.Database.MigrateAsync(ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
