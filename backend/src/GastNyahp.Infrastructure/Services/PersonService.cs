using GastNyahp.Domain.People;
using GastNyahp.Infrastructure.EventStore;
using GastNyahp.Infrastructure.Projections;
using GastNyahp.Infrastructure.Projections.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastNyahp.Infrastructure.Services;

public class PersonService(
    PersonCommandService commands,
    IDbContextFactory<ProjectionsDbContext> dbFactory,
    IReadModelSync sync,
    ILogger<PersonService> logger)
{
    public async Task<List<PersonEntity>> GetAllAsync(Guid familyId, bool includeArchived = false, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.People.Where(p => p.FamilyId == familyId);
        if (!includeArchived) query = query.Where(p => !p.Archived);
        return await query.OrderBy(p => p.Name).ToListAsync(ct);
    }

    public Task<OpResult> RegisterAsync(Guid familyId, string name, string emoji, string color, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        return CommandExecutor.Exec(commands.Handle(new RegisterPerson(id, familyId, name, emoji, color), ct), sync, logger, "RegisterPerson", id, ct);
    }

    public async Task<OpResult> UpdateAsync(Guid familyId, Guid id, string name, string emoji, string color, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("La persona no existe.");
        return await CommandExecutor.Exec(commands.Handle(new UpdatePerson(id, name, emoji, color), ct), sync, logger, "UpdatePerson", id, ct);
    }

    public async Task<OpResult> ArchiveAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("La persona no existe.");
        return await CommandExecutor.Exec(commands.Handle(new ArchivePerson(id), ct), sync, logger, "ArchivePerson", id, ct);
    }

    async Task<bool> OwnedAsync(Guid familyId, Guid id, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.People.AnyAsync(p => p.Id == id && p.FamilyId == familyId, ct);
    }
}
