using GastNyahp.Domain.Budgets;
using GastNyahp.Domain.Income;
using GastNyahp.Infrastructure.EventStore;
using GastNyahp.Infrastructure.Projections;
using GastNyahp.Infrastructure.Projections.Budgets;
using GastNyahp.Infrastructure.Projections.Income;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastNyahp.Infrastructure.Services;

/// <summary>Monthly planning: budget limits, income config, "copy previous month" and the DualPay calculator.</summary>
public class PlanningService(
    BudgetPlanCommandService budgetCommands,
    IncomeCommandService incomeCommands,
    ReserveService reserves,
    IDbContextFactory<ProjectionsDbContext> dbFactory,
    IReadModelSync sync,
    ILogger<PlanningService> logger)
{
    // ── Budget ─────────────────────────────────────────────────────────────────

    /// <summary>All configured months — the frontend keeps budgets as an object keyed by month.</summary>
    public async Task<List<BudgetPlanEntity>> GetAllBudgetsAsync(Guid familyId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.BudgetPlans.Where(b => b.FamilyId == familyId).OrderBy(b => b.Month).ToListAsync(ct);
    }

    public async Task<BudgetPlanEntity> GetBudgetAsync(Guid familyId, string month, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.BudgetPlans.FirstOrDefaultAsync(b => b.FamilyId == familyId && b.Month == month, ct)
            ?? new BudgetPlanEntity { FamilyId = familyId, Month = month }; // zeros = "sin meta configurada"
    }

    /// <summary>Partial semantics like the frontend's setBudget: unspecified fields keep their current value.
    /// The aggregate always receives the full triple (DOMAIN_MODEL.md §11) — the merge happens HERE.</summary>
    public async Task<OpResult> SetBudgetAsync(
        Guid familyId, string month, decimal? creditLimit, decimal? debitCashLimit, decimal? weeklyLimit, CancellationToken ct = default)
    {
        var current = await GetBudgetAsync(familyId, month, ct);
        return await CommandExecutor.Exec(
            budgetCommands.Handle(new SetBudgetLimits(
                familyId,
                month,
                creditLimit ?? current.CreditLimit,
                debitCashLimit ?? current.DebitCashLimit,
                weeklyLimit ?? current.WeeklyLimit), ct),
            sync, logger, "SetBudgetLimits", null, ct);
    }

    // ── Income ─────────────────────────────────────────────────────────────────

    public async Task<IncomeEntity> GetIncomeAsync(Guid familyId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Income.FirstOrDefaultAsync(i => i.FamilyId == familyId, ct)
            ?? new IncomeEntity { FamilyId = familyId };
    }

    public Task<OpResult> UpdateIncomeAsync(
        Guid familyId, decimal? netMonthly, decimal? usdRateOfficial, decimal? usdRateCcl, int? splitPercent, CancellationToken ct = default) =>
        CommandExecutor.Exec(
            incomeCommands.Handle(new UpdateIncome(familyId, netMonthly, usdRateOfficial, usdRateCcl, splitPercent), ct),
            sync, logger, "UpdateIncome", null, ct);

    // ── Copy previous month ─────────────────────────────────────────────────────

    /// <summary>Ported 1:1 from copyMonthData (DOMAIN_MODEL.md §14): estimation data only, never overwrites
    /// the target month, NEVER copies expenses/tickets.</summary>
    public async Task<OpResult> CopyMonthAsync(Guid familyId, string fromMonth, string toMonth, CancellationToken ct = default)
    {
        List<(Guid ReserveId, decimal Amount, string? Note)> reservesToCopy;
        bool copyBudget;
        BudgetPlanEntity? fromBudget;

        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            reservesToCopy = await db.Reserves
                .Where(r => r.FamilyId == familyId && !r.Recurring)
                .Where(r => r.Months.Any(m => m.Month == fromMonth) && !r.Months.Any(m => m.Month == toMonth))
                .Select(r => new ValueTuple<Guid, decimal, string?>(
                    r.Id,
                    r.Months.First(m => m.Month == fromMonth).Amount,
                    r.Months.First(m => m.Month == fromMonth).Note))
                .ToListAsync(ct);

            fromBudget = await db.BudgetPlans.FirstOrDefaultAsync(b => b.FamilyId == familyId && b.Month == fromMonth, ct);
            copyBudget = fromBudget is not null && !await db.BudgetPlans.AnyAsync(b => b.FamilyId == familyId && b.Month == toMonth, ct);
        }

        foreach (var (reserveId, amount, note) in reservesToCopy)
        {
            var result = await reserves.SetMonthAmountAsync(familyId, reserveId, toMonth, amount, note, ct);
            if (!result.Ok) return result;
        }

        if (copyBudget)
        {
            var result = await CommandExecutor.Exec(
                budgetCommands.Handle(new SetBudgetLimits(familyId, toMonth, fromBudget!.CreditLimit, fromBudget.DebitCashLimit, fromBudget.WeeklyLimit), ct),
                sync, logger, "SetBudgetLimits(copy)", null, ct);
            if (!result.Ok) return result;
        }

        return OpResult.Success();
    }

    // ── DualPay (pure calculation, nothing persisted — DOMAIN_MODEL.md §14) ────

    public static DualPayPreview CalculateDualPay(decimal grossNet, decimal usdRateOfficial, decimal usdRateCcl)
    {
        // 30/70 split hardcoded on purpose — the frontend ignores income.splitPercent here too (legacy quirk).
        var pesos = Math.Round(grossNet * 0.30m);
        var usd = usdRateOfficial > 0 ? grossNet * 0.70m / usdRateOfficial : 0;
        var ccl = Math.Round(usd * usdRateCcl);
        return new DualPayPreview(pesos, Math.Round(usd, 2), ccl, pesos + ccl);
    }
}

public record DualPayPreview(decimal Pesos, decimal Usd, decimal Ccl, decimal Total);
