using GastNyahp.Domain.BusinessDays;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Infrastructure.Projections.BusinessDays;

public class BusinessDayProjection : GastNyahpProjection
{
    readonly IDbContextFactory<ProjectionsDbContext> _dbFactory;

    public BusinessDayProjection(IDbContextFactory<ProjectionsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        On<BusinessDayEvents.V1.BusinessDayOpened>(ctx => new ValueTask(HandleOpened(ctx.Message, ctx.CancellationToken)));
    }

    public async Task HandleOpened(BusinessDayEvents.V1.BusinessDayOpened e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.BusinessDays.AnyAsync(d => d.Date == e.Date, ct)) return;

        db.BusinessDays.Add(new BusinessDayEntity { Date = e.Date, OpenedAt = DateTime.Parse(e.OpenedAt).ToUniversalTime() });
        await SaveIgnoringDuplicate(db, ct);
    }
}
