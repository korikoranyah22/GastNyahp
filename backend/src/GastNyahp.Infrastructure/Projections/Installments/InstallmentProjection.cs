using GastNyahp.Domain.Common;
using GastNyahp.Domain.Installments;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Infrastructure.Projections.Installments;

public class InstallmentProjection : GastNyahpProjection
{
    const string Prefix = "installment";
    readonly IDbContextFactory<ProjectionsDbContext> _dbFactory;

    public InstallmentProjection(IDbContextFactory<ProjectionsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        On<InstallmentEvents.V1.InstallmentPurchaseRegistered>(ctx => new ValueTask(HandleRegistered(ctx.Message, ctx.CancellationToken)));
        On<InstallmentEvents.V1.InstallmentScheduleRevised>(ctx => new ValueTask(HandleScheduleRevised(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<InstallmentEvents.V1.InstallmentDetailsUpdated>(ctx => new ValueTask(HandleDetailsUpdated(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<InstallmentEvents.V1.InstallmentMonthAmountOverridden>(ctx => new ValueTask(HandleMonthAmountOverridden(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<InstallmentEvents.V1.InstallmentMonthPaidToggled>(ctx => new ValueTask(HandleMonthPaidToggled(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<InstallmentEvents.V1.InstallmentFinished>(ctx => new ValueTask(HandleFinished(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.CancellationToken)));
        On<InstallmentEvents.V1.InstallmentRemoved>(ctx => new ValueTask(HandleRemoved(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.CancellationToken)));
    }

    public async Task HandleRegistered(InstallmentEvents.V1.InstallmentPurchaseRegistered e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.InstallmentPurchases.AnyAsync(i => i.Id == e.InstallmentId, ct)) return;

        // Same calendar-generation rule the aggregate State uses — MonthlySchedule is the single source of truth.
        var count = e.Frequency == InstallmentFrequency.Monthly ? InstallmentDefaults.MonthlyRecurringWindow : e.TotalInstallments!.Value;
        var months = MonthlySchedule.Generate(YearMonth.Parse(e.StartMonth), count, e.MonthlyAmount);

        db.InstallmentPurchases.Add(new InstallmentPurchaseEntity
        {
            Id = e.InstallmentId, FamilyId = e.FamilyId, CardId = e.CardId, Description = e.Description, Category = e.Category,
            PurchaseDate = e.PurchaseDate, Frequency = e.Frequency.ToString(), MonthlyAmount = e.MonthlyAmount,
            TotalInstallments = e.TotalInstallments, StartMonth = e.StartMonth,
            OwnerKind = e.OwnerKind, OwnerPersonId = e.OwnerPersonId, Active = true, UpdatedAt = DateTime.UtcNow,
            Months = months.Select(m => new InstallmentMonthEntity
            {
                InstallmentId = e.InstallmentId, Month = m.Month.ToString(), Amount = m.Amount, Paid = m.Paid, UpdatedAt = DateTime.UtcNow,
            }).ToList(),
        });
        await SaveIgnoringDuplicate(db, ct);
    }

    public async Task HandleScheduleRevised(Guid installmentId, InstallmentEvents.V1.InstallmentScheduleRevised e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.InstallmentPurchases.Include(i => i.Months).FirstOrDefaultAsync(i => i.Id == installmentId, ct);
        if (entity is null) return;

        var existing = entity.Months
            .Select(m => new ScheduleMonth(YearMonth.Parse(m.Month), m.Amount, m.Paid))
            .ToList();
        var count = e.Frequency == InstallmentFrequency.Monthly ? InstallmentDefaults.MonthlyRecurringWindow : e.TotalInstallments!.Value;
        var revised = MonthlySchedule.Revise(existing, YearMonth.Parse(e.StartMonth), count, e.MonthlyAmount);

        entity.StartMonth = e.StartMonth;
        entity.TotalInstallments = e.TotalInstallments;
        entity.Frequency = e.Frequency.ToString();
        entity.MonthlyAmount = e.MonthlyAmount;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.Months.Clear();
        foreach (var m in revised)
        {
            entity.Months.Add(new InstallmentMonthEntity
            {
                InstallmentId = installmentId, Month = m.Month.ToString(), Amount = m.Amount, Paid = m.Paid, UpdatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task HandleDetailsUpdated(Guid installmentId, InstallmentEvents.V1.InstallmentDetailsUpdated e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.InstallmentPurchases.FirstOrDefaultAsync(i => i.Id == installmentId, ct);
        if (entity is null) return;

        entity.Description = e.Description;
        entity.Category = e.Category;
        entity.PurchaseDate = e.PurchaseDate;
        entity.OwnerKind = e.OwnerKind;
        entity.OwnerPersonId = e.OwnerPersonId;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleMonthAmountOverridden(Guid installmentId, InstallmentEvents.V1.InstallmentMonthAmountOverridden e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var month = await db.InstallmentMonths.FirstOrDefaultAsync(m => m.InstallmentId == installmentId && m.Month == e.Month, ct);
        if (month is null) return;

        month.Amount = e.Amount;
        month.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleMonthPaidToggled(Guid installmentId, InstallmentEvents.V1.InstallmentMonthPaidToggled e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var month = await db.InstallmentMonths.FirstOrDefaultAsync(m => m.InstallmentId == installmentId && m.Month == e.Month, ct);
        if (month is null) return;

        month.Paid = !month.Paid;
        month.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleFinished(Guid installmentId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.InstallmentPurchases.FirstOrDefaultAsync(i => i.Id == installmentId, ct);
        if (entity is null) return;

        entity.Active = false;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleRemoved(Guid installmentId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        // Months go with it via the FK cascade configured in ProjectionsDbContext.
        await db.InstallmentPurchases.Where(i => i.Id == installmentId).ExecuteDeleteAsync(ct);
    }
}
