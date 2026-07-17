using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace GastNyahp.E2E.Tests.Support;

/// <summary>
/// Boots the REAL GastNyahp.Api host in memory with the two infrastructure substitutions described in the
/// reqnroll-e2e-api-tests skill: InMemory event store and a named shared in-memory SQLite database. The
/// keep-alive connection is what keeps the database alive across the factory's short-lived DbContexts.
/// </summary>
public sealed class GastNyahpApiFactory : WebApplicationFactory<Program>
{
    readonly SqliteConnection _keepAlive;

    public GastNyahpApiFactory()
    {
        var dbName = $"e2e-{Guid.NewGuid():N}";
        ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";
        _keepAlive = new SqliteConnection(ConnectionString);
        _keepAlive.Open();
    }

    public string ConnectionString { get; }

    public const string AdminKey = "e2e-admin-key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("E2E");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Projections", ConnectionString);
        builder.UseSetting("EventStore:Provider", "InMemory");
        builder.UseSetting("Admin:ApiKey", AdminKey);
        builder.UseSetting("BusinessDay:Enabled", "false"); // scenarios open their own dates explicitly
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _keepAlive.Dispose();
    }
}
