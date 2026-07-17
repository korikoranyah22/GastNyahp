using GastNyahp.Domain.Cards;
using GastNyahp.Infrastructure.EventStore;
using GastNyahp.Infrastructure.Projections;
using GastNyahp.Infrastructure.Projections.Cards;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastNyahp.Infrastructure.Services;

public class CardService(
    CreditCardCommandService commands,
    IDbContextFactory<ProjectionsDbContext> dbFactory,
    IReadModelSync sync,
    ILogger<CardService> logger)
{
    public async Task<List<CreditCardEntity>> GetAllAsync(Guid familyId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CreditCards.Where(c => c.FamilyId == familyId).OrderBy(c => c.Label).ToListAsync(ct);
    }

    public async Task<CreditCardEntity?> GetByIdAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CreditCards.FirstOrDefaultAsync(c => c.Id == id && c.FamilyId == familyId, ct);
    }

    public async Task<OpResult> RegisterAsync(
        Guid familyId, Guid bankId, string label, CardNetwork network, CardType type, int closingDay, int dueDay, string color, CancellationToken ct = default)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            if (!await db.Banks.AnyAsync(b => b.Id == bankId && b.FamilyId == familyId, ct))
                return OpResult.Fail("El banco no existe.");
        }

        var id = Guid.NewGuid();
        return await CommandExecutor.Exec(
            commands.Handle(new RegisterCard(id, familyId, bankId, label, network, type, closingDay, dueDay, color), ct),
            sync, logger, "RegisterCard", id, ct);
    }

    public async Task<OpResult> UpdateAsync(
        Guid familyId, Guid id, string label, CardNetwork network, CardType type, int closingDay, int dueDay, string color, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("La tarjeta no existe.");
        return await CommandExecutor.Exec(commands.Handle(new UpdateCard(id, label, network, type, closingDay, dueDay, color), ct), sync, logger, "UpdateCard", id, ct);
    }

    public async Task<OpResult> ActivateAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("La tarjeta no existe.");
        return await CommandExecutor.Exec(commands.Handle(new ActivateCard(id), ct), sync, logger, "ActivateCard", id, ct);
    }

    public async Task<OpResult> DeactivateAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("La tarjeta no existe.");
        return await CommandExecutor.Exec(commands.Handle(new DeactivateCard(id), ct), sync, logger, "DeactivateCard", id, ct);
    }

    public async Task<OpResult> RemoveAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("La tarjeta no existe.");

        // Deliberately STRONGER than the legacy frontend (DOMAIN_MODEL.md §3 decision #2).
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var hasInstallments = await db.InstallmentPurchases.AnyAsync(i => i.CardId == id, ct);
            var hasLinkedServices = await db.Services.AnyAsync(s => s.LinkedCardId == id, ct);
            if (hasInstallments || hasLinkedServices)
                return OpResult.Fail("La tarjeta tiene cuotas o servicios asociados.");
        }

        return await CommandExecutor.Exec(commands.Handle(new RemoveCard(id), ct), sync, logger, "RemoveCard", id, ct);
    }

    async Task<bool> OwnedAsync(Guid familyId, Guid id, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.CreditCards.AnyAsync(c => c.Id == id && c.FamilyId == familyId, ct);
    }
}
