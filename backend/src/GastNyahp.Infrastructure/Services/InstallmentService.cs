using GastNyahp.Domain.Common;
using GastNyahp.Domain.Installments;
using GastNyahp.Infrastructure.EventStore;
using GastNyahp.Infrastructure.Projections;
using GastNyahp.Infrastructure.Projections.Installments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastNyahp.Infrastructure.Services;

public class InstallmentService(
    InstallmentPurchaseCommandService commands,
    IDbContextFactory<ProjectionsDbContext> dbFactory,
    IReadModelSync sync,
    ILogger<InstallmentService> logger)
{
    public async Task<List<InstallmentPurchaseEntity>> GetAllAsync(Guid familyId, Guid? cardId = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = db.InstallmentPurchases.Include(i => i.Months).Where(i => i.FamilyId == familyId);
        if (cardId is not null) query = query.Where(i => i.CardId == cardId);
        return await query.OrderBy(i => i.Description).ToListAsync(ct);
    }

    public async Task<InstallmentPurchaseEntity?> GetByIdAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.InstallmentPurchases.Include(i => i.Months).FirstOrDefaultAsync(i => i.Id == id && i.FamilyId == familyId, ct);
    }

    public async Task<OpResult> RegisterAsync(
        Guid familyId, Guid cardId, string description, string category, string purchaseDate, InstallmentFrequency frequency,
        decimal monthlyAmount, int? totalInstallments, string startMonth, OwnerRef owner, CancellationToken ct = default)
    {
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            if (!await db.CreditCards.AnyAsync(c => c.Id == cardId && c.FamilyId == familyId, ct))
                return OpResult.Fail("La tarjeta no existe.");
        }

        var id = Guid.NewGuid();
        return await CommandExecutor.Exec(
            commands.Handle(new RegisterInstallmentPurchase(id, familyId, cardId, description, category, purchaseDate, frequency, monthlyAmount, totalInstallments, startMonth, owner), ct),
            sync, logger, "RegisterInstallmentPurchase", id, ct);
    }

    public async Task<OpResult> UpdateDetailsAsync(Guid familyId, Guid id, string description, string category, string purchaseDate, OwnerRef owner, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("La compra en cuotas no existe.");
        return await CommandExecutor.Exec(commands.Handle(new UpdateInstallmentDetails(id, description, category, purchaseDate, owner), ct), sync, logger, "UpdateInstallmentDetails", id, ct);
    }

    public async Task<OpResult> ReviseScheduleAsync(Guid familyId, Guid id, string startMonth, int? totalInstallments, InstallmentFrequency frequency, decimal monthlyAmount, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("La compra en cuotas no existe.");
        return await CommandExecutor.Exec(commands.Handle(new ReviseInstallmentSchedule(id, startMonth, totalInstallments, frequency, monthlyAmount), ct), sync, logger, "ReviseInstallmentSchedule", id, ct);
    }

    public async Task<OpResult> OverrideMonthAmountAsync(Guid familyId, Guid id, string month, decimal amount, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("La compra en cuotas no existe.");
        return await CommandExecutor.Exec(commands.Handle(new OverrideInstallmentMonthAmount(id, month, amount), ct), sync, logger, "OverrideInstallmentMonthAmount", id, ct);
    }

    public async Task<OpResult> ToggleMonthPaidAsync(Guid familyId, Guid id, string month, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("La compra en cuotas no existe.");
        return await CommandExecutor.Exec(commands.Handle(new ToggleInstallmentMonthPaid(id, month), ct), sync, logger, "ToggleInstallmentMonthPaid", id, ct);
    }

    public async Task<OpResult> FinishAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("La compra en cuotas no existe.");
        return await CommandExecutor.Exec(commands.Handle(new FinishInstallment(id), ct), sync, logger, "FinishInstallment", id, ct);
    }

    public async Task<OpResult> RemoveAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("La compra en cuotas no existe.");
        return await CommandExecutor.Exec(commands.Handle(new RemoveInstallmentPurchase(id), ct), sync, logger, "RemoveInstallmentPurchase", id, ct);
    }

    async Task<bool> OwnedAsync(Guid familyId, Guid id, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.InstallmentPurchases.AnyAsync(i => i.Id == id && i.FamilyId == familyId, ct);
    }
}
