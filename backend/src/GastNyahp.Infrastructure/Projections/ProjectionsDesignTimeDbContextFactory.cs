using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GastNyahp.Infrastructure.Projections;

/// <summary>Lets `dotnet ef migrations add` construct the DbContext without booting the full app. Connection
/// string comes from an env var, never hardcoded — see ef-core-postgres-context.</summary>
public class ProjectionsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ProjectionsDbContext>
{
    public ProjectionsDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Projections")
            ?? "Host=localhost;Port=5432;Database=gastnyahpdb;Username=gastnyahp;Password=dummy_for_migration_scaffold";

        var options = new DbContextOptionsBuilder<ProjectionsDbContext>().UseNpgsql(cs).Options;
        return new ProjectionsDbContext(options);
    }
}
