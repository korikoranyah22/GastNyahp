using GastNyahp.Domain.Common;
using GastNyahp.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Infrastructure.Projections.Services;

public class ServiceProjection : GastNyahpProjection
{
    const string Prefix = "service";
    readonly IDbContextFactory<ProjectionsDbContext> _dbFactory;

    public ServiceProjection(IDbContextFactory<ProjectionsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        On<ServiceEvents.V1.ServiceRegistered>(ctx => new ValueTask(HandleRegistered(ctx.Message, ctx.CancellationToken)));
        On<ServiceEvents.V1.ServiceDetailsUpdated>(ctx => new ValueTask(HandleDetailsUpdated(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<ServiceEvents.V1.ServiceActivated>(ctx => new ValueTask(HandleActiveChanged(StreamIds.GuidFrom(ctx.Stream, Prefix), active: true, ctx.CancellationToken)));
        On<ServiceEvents.V1.ServiceDeactivated>(ctx => new ValueTask(HandleActiveChanged(StreamIds.GuidFrom(ctx.Stream, Prefix), active: false, ctx.CancellationToken)));
        On<ServiceEvents.V1.ServiceMonthAmountSet>(ctx => new ValueTask(HandleMonthAmountSet(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<ServiceEvents.V1.ServiceFutureAmountsExtended>(ctx => new ValueTask(HandleFutureAmountsExtended(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<ServiceEvents.V1.ServiceMonthPaidToggled>(ctx => new ValueTask(HandleMonthPaidToggled(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<ServiceEvents.V1.ServiceRemoved>(ctx => new ValueTask(HandleRemoved(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.CancellationToken)));
    }

    public async Task HandleRegistered(ServiceEvents.V1.ServiceRegistered e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.Services.AnyAsync(s => s.Id == e.ServiceId, ct)) return;

        // 12 months forward from the registration month, same rule as the aggregate State (DOMAIN_MODEL.md §6).
        var months = YearMonth.Parse(e.RegisteredFromMonth).Take(12);

        db.Services.Add(new ServiceEntity
        {
            Id = e.ServiceId, FamilyId = e.FamilyId, Name = e.Name, Category = e.Category, BillingType = e.BillingType.ToString(),
            LinkedCardId = e.LinkedCardId, Active = true, Currency = e.Currency.ToString(),
            OriginalAmount = e.OriginalAmount, OriginalCurrency = e.OriginalCurrency?.ToString(),
            OwnerKind = e.OwnerKind, OwnerPersonId = e.OwnerPersonId, UpdatedAt = DateTime.UtcNow,
            Amounts = months.Select(m => new ServiceMonthAmountEntity
            {
                ServiceId = e.ServiceId, Month = m.ToString(), AmountArs = e.BaseAmountArs, Paid = false, UpdatedAt = DateTime.UtcNow,
            }).ToList(),
        });
        await SaveIgnoringDuplicate(db, ct);
    }

    public async Task HandleDetailsUpdated(Guid serviceId, ServiceEvents.V1.ServiceDetailsUpdated e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Services.FirstOrDefaultAsync(s => s.Id == serviceId, ct);
        if (entity is null) return;

        entity.Name = e.Name;
        entity.Category = e.Category;
        entity.BillingType = e.BillingType.ToString();
        entity.LinkedCardId = e.LinkedCardId;


        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleActiveChanged(Guid serviceId, bool active, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Services.FirstOrDefaultAsync(s => s.Id == serviceId, ct);
        if (entity is null) return;

        entity.Active = active;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleMonthAmountSet(Guid serviceId, ServiceEvents.V1.ServiceMonthAmountSet e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Services.FirstOrDefaultAsync(s => s.Id == serviceId, ct);
        if (entity is null) return;

        entity.OriginalAmount = e.OriginalAmount;
        entity.OriginalCurrency = e.OriginalCurrency?.ToString();
        entity.UpdatedAt = DateTime.UtcNow;
        await UpsertMonth(db, serviceId, e.Month, e.AmountArs, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleFutureAmountsExtended(Guid serviceId, ServiceEvents.V1.ServiceFutureAmountsExtended e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (!await db.Services.AnyAsync(s => s.Id == serviceId, ct)) return;

        foreach (var month in YearMonth.Parse(e.FromMonth).Take(e.MonthsAhead))
            await UpsertMonth(db, serviceId, month.ToString(), e.AmountArs, ct);

        await db.SaveChangesAsync(ct);
    }

    public async Task HandleMonthPaidToggled(Guid serviceId, ServiceEvents.V1.ServiceMonthPaidToggled e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (!await db.Services.AnyAsync(s => s.Id == serviceId, ct)) return;

        var month = await db.ServiceMonthAmounts.FirstOrDefaultAsync(m => m.ServiceId == serviceId && m.Month == e.Month, ct);
        if (month is null)
        {
            // Marking an unloaded month as paid creates it with a zero amount — same edge case the frontend supports.
            db.ServiceMonthAmounts.Add(new ServiceMonthAmountEntity
            {
                ServiceId = serviceId, Month = e.Month, AmountArs = 0, Paid = true, UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            month.Paid = !month.Paid;
            month.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleRemoved(Guid serviceId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Services.Where(s => s.Id == serviceId).ExecuteDeleteAsync(ct);
    }

    /// <summary>Upsert preserving the Paid flag of an existing month — amount always updates.</summary>
    static async Task UpsertMonth(ProjectionsDbContext db, Guid serviceId, string month, decimal amountArs, CancellationToken ct)
    {
        var existing = db.ServiceMonthAmounts.Local.FirstOrDefault(m => m.ServiceId == serviceId && m.Month == month)
            ?? await db.ServiceMonthAmounts.FirstOrDefaultAsync(m => m.ServiceId == serviceId && m.Month == month, ct);
        if (existing is null)
        {
            db.ServiceMonthAmounts.Add(new ServiceMonthAmountEntity
            {
                ServiceId = serviceId, Month = month, AmountArs = amountArs, Paid = false, UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.AmountArs = amountArs;
            existing.UpdatedAt = DateTime.UtcNow;
        }
    }
}
