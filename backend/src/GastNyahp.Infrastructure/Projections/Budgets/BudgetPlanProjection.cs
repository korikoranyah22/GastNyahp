using GastNyahp.Domain.Budgets;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Infrastructure.Projections.Budgets;

public class BudgetPlanProjection : GastNyahpProjection
{
    readonly IDbContextFactory<ProjectionsDbContext> _dbFactory;

    public BudgetPlanProjection(IDbContextFactory<ProjectionsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        // The event carries its own natural key (Month) — no stream parsing needed.
        On<BudgetPlanEvents.V1.BudgetLimitsSet>(ctx => new ValueTask(HandleLimitsSet(ctx.Message, ctx.CancellationToken)));
    }

    public async Task HandleLimitsSet(BudgetPlanEvents.V1.BudgetLimitsSet e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.BudgetPlans.FirstOrDefaultAsync(b => b.FamilyId == e.FamilyId && b.Month == e.Month, ct);
        if (entity is null)
        {
            db.BudgetPlans.Add(new BudgetPlanEntity
            {
                FamilyId = e.FamilyId, Month = e.Month, CreditLimit = e.CreditLimit, DebitCashLimit = e.DebitCashLimit,
                WeeklyLimit = e.WeeklyLimit, UpdatedAt = DateTime.UtcNow,
            });
            await SaveIgnoringDuplicate(db, ct);
            return;
        }

        entity.CreditLimit = e.CreditLimit;
        entity.DebitCashLimit = e.DebitCashLimit;
        entity.WeeklyLimit = e.WeeklyLimit;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
