using GastNyahp.Domain.Income;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Infrastructure.Projections.Income;

public class IncomeProjection : GastNyahpProjection
{
    readonly IDbContextFactory<ProjectionsDbContext> _dbFactory;

    public IncomeProjection(IDbContextFactory<ProjectionsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        On<IncomeEvents.V1.IncomeUpdated>(ctx => new ValueTask(HandleUpdated(ctx.Message, ctx.CancellationToken)));
    }

    public async Task HandleUpdated(IncomeEvents.V1.IncomeUpdated e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Income.FirstOrDefaultAsync(i => i.FamilyId == e.FamilyId, ct);
        if (entity is null)
        {
            entity = new IncomeEntity { FamilyId = e.FamilyId, SplitPercent = 70 };
            db.Income.Add(entity);
        }

        // Partial merge, same as the aggregate State fold: only provided fields change.
        entity.NetMonthly = e.NetMonthly ?? entity.NetMonthly;
        entity.UsdRateOfficial = e.UsdRateOfficial ?? entity.UsdRateOfficial;
        entity.UsdRateCcl = e.UsdRateCcl ?? entity.UsdRateCcl;
        entity.SplitPercent = e.SplitPercent ?? entity.SplitPercent;
        entity.UpdatedAt = DateTime.UtcNow;

        // Append-only history with the RESULTING (merged) values, so "what was the income in month X" is a
        // simple lookup by ChangedAt — DOMAIN_MODEL.md §12.
        db.IncomeHistory.Add(new IncomeHistoryEntity
        {
            FamilyId = e.FamilyId,
            ChangedAt = DateTime.UtcNow,
            NetMonthly = entity.NetMonthly,
            UsdRateOfficial = entity.UsdRateOfficial,
            UsdRateCcl = entity.UsdRateCcl,
            SplitPercent = entity.SplitPercent,
        });

        await db.SaveChangesAsync(ct);
    }
}
