using GastNyahp.Domain.Banks;
using GastNyahp.Infrastructure.EventStore;
using GastNyahp.Infrastructure.Projections;
using GastNyahp.Infrastructure.Projections.Banks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastNyahp.Infrastructure.Services;

public class BankService(
    BankCommandService commands,
    IDbContextFactory<ProjectionsDbContext> dbFactory,
    IReadModelSync sync,
    ILogger<BankService> logger)
{
    public async Task<List<BankEntity>> GetAllAsync(Guid familyId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Banks.Where(b => b.FamilyId == familyId).OrderBy(b => b.Name).ToListAsync(ct);
    }

    public async Task<BankEntity?> GetByIdAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Banks.FirstOrDefaultAsync(b => b.Id == id && b.FamilyId == familyId, ct);
    }

    public Task<OpResult> RegisterAsync(Guid familyId, string name, string? alias, string color, string icon, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        return CommandExecutor.Exec(commands.Handle(new RegisterBank(id, familyId, name, alias, color, icon), ct), sync, logger, "RegisterBank", id, ct);
    }

    public async Task<OpResult> UpdateAsync(Guid familyId, Guid id, string name, string? alias, string color, string icon, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("El banco no existe.");
        return await CommandExecutor.Exec(commands.Handle(new UpdateBank(id, name, alias, color, icon), ct), sync, logger, "UpdateBank", id, ct);
    }

    public async Task<OpResult> RemoveAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("El banco no existe.");

        // Cross-aggregate invariant (DOMAIN_MODEL.md §2/§16) — same message the frontend shows today.
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var hasCards = await db.CreditCards.AnyAsync(c => c.BankId == id, ct);
            var hasLoans = await db.Loans.AnyAsync(l => l.BankId == id, ct);
            if (hasCards || hasLoans)
                return OpResult.Fail("El banco tiene tarjetas o préstamos asociados.");
        }

        return await CommandExecutor.Exec(commands.Handle(new RemoveBank(id), ct), sync, logger, "RemoveBank", id, ct);
    }

    async Task<bool> OwnedAsync(Guid familyId, Guid id, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Banks.AnyAsync(b => b.Id == id && b.FamilyId == familyId, ct);
    }
}
