using GastNyahp.Domain.Common;
using GastNyahp.Domain.Expenses;
using GastNyahp.Infrastructure.EventStore;
using GastNyahp.Infrastructure.Projections;
using GastNyahp.Infrastructure.Projections.Expenses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastNyahp.Infrastructure.Services;

public class ExpenseService(
    ExpenseCommandService commands,
    IDbContextFactory<ProjectionsDbContext> dbFactory,
    IReadModelSync sync,
    ILogger<ExpenseService> logger)
{
    /// <summary>Calendar-month listing — the ExpensesPage semantics, not the billing-month one (§10.1).</summary>
    public async Task<List<ExpenseEntity>> GetByMonthAsync(Guid familyId, string month, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Expenses.Where(e => e.FamilyId == familyId && e.Date.StartsWith(month)).OrderBy(e => e.Date).ToListAsync(ct);
    }

    public async Task<ExpenseEntity?> GetByIdAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.FamilyId == familyId, ct);
    }

    public async Task<OpResult> RegisterAsync(
        Guid familyId, string date, string description, string category, decimal amount, ExpenseCurrency currency,
        PaymentMethod paymentMethod, OwnerRef owner, CancellationToken ct = default)
    {
        var usdRateCcl = await ReadUsdRateCcl(familyId, ct);
        var id = Guid.NewGuid();
        return await CommandExecutor.Exec(
            commands.Handle(new RegisterExpense(id, familyId, date, description, category, amount, currency, paymentMethod, owner, usdRateCcl), ct),
            sync, logger, "RegisterExpense", id, ct);
    }

    public async Task<OpResult> UpdateAsync(
        Guid familyId, Guid id, string date, string description, string category, decimal amount, ExpenseCurrency currency,
        PaymentMethod paymentMethod, OwnerRef owner, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("El gasto no existe.");
        var usdRateCcl = await ReadUsdRateCcl(familyId, ct);
        return await CommandExecutor.Exec(
            commands.Handle(new UpdateExpense(id, date, description, category, amount, currency, paymentMethod, owner, usdRateCcl), ct),
            sync, logger, "UpdateExpense", id, ct);
    }

    public async Task<OpResult> RemoveAsync(Guid familyId, Guid id, CancellationToken ct = default)
    {
        if (!await OwnedAsync(familyId, id, ct)) return OpResult.Fail("El gasto no existe.");
        return await CommandExecutor.Exec(commands.Handle(new RemoveExpense(id), ct), sync, logger, "RemoveExpense", id, ct);
    }

    async Task<decimal> ReadUsdRateCcl(Guid familyId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await ServicesService.GetUsdRateCcl(db, familyId, ct);
    }

    async Task<bool> OwnedAsync(Guid familyId, Guid id, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Expenses.AnyAsync(e => e.Id == id && e.FamilyId == familyId, ct);
    }
}
