using GastNyahp.Domain.Common;
using GastNyahp.Domain.Services;
using GastNyahp.Infrastructure.EventStore;
using GastNyahp.Infrastructure.Projections;
using GastNyahp.Infrastructure.Projections.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastNyahp.Infrastructure.Services;

/// <summary>App service for the "Service" aggregate (servicios recurrentes) — doubled name is deliberate,
/// the domain word IS "Service".</summary>
public class ServicesService(
    ServiceCommandService commands,
    IDbContextFactory<ProjectionsDbContext> dbFactory,
    IReadModelSync sync,
    ILogger<ServicesService> logger)
{
    public async Task<List<ServiceEntity>> GetAllAsync(Guid familyId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Services.Include(s => s.Amounts).Where(s => s.FamilyId == familyId).OrderBy(s => s.Name).ToListAsync(ct);
    }

    public async Task<ServiceEntity?> GetByIdAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Services.Include(s => s.Amounts).FirstOrDefaultAsync(s => s.Id == id && s.FamilyId == familyId, ct);
    }

    public async Task<OpResult> RegisterAsync(
        Guid familyId, string name, string category, BillingType billingType, Guid? linkedCardId, ServiceCurrency currency,
        decimal baseAmount, string registeredFromMonth, OwnerRef owner, CancellationToken ct = default)
    {
        decimal usdRateCcl;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            if (linkedCardId is not null && !await db.CreditCards.AnyAsync(c => c.Id == linkedCardId && c.FamilyId == familyId, ct))
                return OpResult.Fail("La tarjeta vinculada no existe.");
            usdRateCcl = await GetUsdRateCcl(db, familyId, ct);
        }

        var id = Guid.NewGuid();
        return await CommandExecutor.Exec(
            commands.Handle(new RegisterService(id, familyId, name, category, billingType, linkedCardId, currency, baseAmount, registeredFromMonth, owner, usdRateCcl), ct),
            sync, logger, "RegisterService", id, ct);
    }

    public async Task<OpResult> UpdateDetailsAsync(Guid familyId, Guid id, string name, string category, BillingType billingType, Guid? linkedCardId, ServiceCurrency currency, OwnerRef? owner = null, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("El servicio no existe.");
        return await CommandExecutor.Exec(commands.Handle(new UpdateServiceDetails(id, name, category, billingType, linkedCardId, currency, owner), ct), sync, logger, "UpdateServiceDetails", id, ct);
    }

    public async Task<OpResult> SetMonthAmountAsync(Guid familyId, Guid id, string month, decimal amount, ServiceCurrency currency, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("El servicio no existe.");

        decimal usdRateCcl;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
            usdRateCcl = await GetUsdRateCcl(db, familyId, ct);

        return await CommandExecutor.Exec(
            commands.Handle(new SetServiceMonthAmount(id, month, amount, currency, usdRateCcl), ct), sync, logger, "SetServiceMonthAmount", id, ct);
    }

    public async Task<OpResult> ExtendFutureAmountsAsync(Guid familyId, Guid id, string fromMonth, decimal amountArs, int monthsAhead, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("El servicio no existe.");
        return await CommandExecutor.Exec(commands.Handle(new ExtendServiceFutureAmounts(id, fromMonth, amountArs, monthsAhead), ct), sync, logger, "ExtendServiceFutureAmounts", id, ct);
    }

    public async Task<OpResult> ToggleMonthPaidAsync(Guid familyId, Guid id, string month, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("El servicio no existe.");
        return await CommandExecutor.Exec(commands.Handle(new ToggleServiceMonthPaid(id, month), ct), sync, logger, "ToggleServiceMonthPaid", id, ct);
    }

    public async Task<OpResult> ActivateAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("El servicio no existe.");
        return await CommandExecutor.Exec(commands.Handle(new ActivateService(id), ct), sync, logger, "ActivateService", id, ct);
    }

    public async Task<OpResult> DeactivateAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("El servicio no existe.");
        return await CommandExecutor.Exec(commands.Handle(new DeactivateService(id), ct), sync, logger, "DeactivateService", id, ct);
    }

    public async Task<OpResult> RemoveAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("El servicio no existe.");
        return await CommandExecutor.Exec(commands.Handle(new RemoveService(id), ct), sync, logger, "RemoveService", id, ct);
    }

    /// <summary>USD → ARS uses the family's configured CCL rate (DOMAIN_MODEL.md §6/§16).</summary>
    internal static async Task<decimal> GetUsdRateCcl(ProjectionsDbContext db, Guid familyId, CancellationToken ct)
    {
        var income = await db.Income.FirstOrDefaultAsync(i => i.FamilyId == familyId, ct);
        return income?.UsdRateCcl ?? 0;
    }

    async Task<bool> OwnedAsync(Guid familyId, Guid id, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Services.AnyAsync(s => s.Id == id && s.FamilyId == familyId, ct);
    }
}
