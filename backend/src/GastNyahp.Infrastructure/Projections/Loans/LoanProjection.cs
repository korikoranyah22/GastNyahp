using GastNyahp.Domain.Common;
using GastNyahp.Domain.Loans;
using Microsoft.EntityFrameworkCore;

namespace GastNyahp.Infrastructure.Projections.Loans;

public class LoanProjection : GastNyahpProjection
{
    const string Prefix = "loan";
    readonly IDbContextFactory<ProjectionsDbContext> _dbFactory;

    public LoanProjection(IDbContextFactory<ProjectionsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        On<LoanEvents.V1.LoanRegistered>(ctx => new ValueTask(HandleRegistered(ctx.Message, ctx.CancellationToken)));
        On<LoanEvents.V1.LoanScheduleRevised>(ctx => new ValueTask(HandleScheduleRevised(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<LoanEvents.V1.LoanDetailsUpdated>(ctx => new ValueTask(HandleDetailsUpdated(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<LoanEvents.V1.LoanMonthAmountOverridden>(ctx => new ValueTask(HandleMonthAmountOverridden(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<LoanEvents.V1.LoanMonthPaidToggled>(ctx => new ValueTask(HandleMonthPaidToggled(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.Message, ctx.CancellationToken)));
        On<LoanEvents.V1.LoanRemoved>(ctx => new ValueTask(HandleRemoved(StreamIds.GuidFrom(ctx.Stream, Prefix), ctx.CancellationToken)));
    }

    public async Task HandleRegistered(LoanEvents.V1.LoanRegistered e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        if (await db.Loans.AnyAsync(l => l.Id == e.LoanId, ct)) return;

        var months = MonthlySchedule.Generate(YearMonth.Parse(e.StartMonth), e.TotalInstallments, e.MonthlyInstallment);

        db.Loans.Add(new LoanEntity
        {
            Id = e.LoanId, FamilyId = e.FamilyId, BankId = e.BankId, Description = e.Description, TotalAmount = e.TotalAmount,
            MonthlyInstallment = e.MonthlyInstallment, StartMonth = e.StartMonth, TotalInstallments = e.TotalInstallments,
            PaidInstallments = 0, UpdatedAt = DateTime.UtcNow,
            Months = months.Select(m => new LoanMonthEntity
            {
                LoanId = e.LoanId, Month = m.Month.ToString(), Amount = m.Amount, Paid = m.Paid, UpdatedAt = DateTime.UtcNow,
            }).ToList(),
        });
        await SaveIgnoringDuplicate(db, ct);
    }

    public async Task HandleScheduleRevised(Guid loanId, LoanEvents.V1.LoanScheduleRevised e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Loans.Include(l => l.Months).FirstOrDefaultAsync(l => l.Id == loanId, ct);
        if (entity is null) return;

        var existing = entity.Months
            .Select(m => new ScheduleMonth(YearMonth.Parse(m.Month), m.Amount, m.Paid))
            .ToList();
        var revised = MonthlySchedule.Revise(existing, YearMonth.Parse(e.StartMonth), e.TotalInstallments, e.MonthlyInstallment);

        entity.StartMonth = e.StartMonth;
        entity.TotalInstallments = e.TotalInstallments;
        entity.MonthlyInstallment = e.MonthlyInstallment;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.Months.Clear();
        foreach (var m in revised)
        {
            entity.Months.Add(new LoanMonthEntity
            {
                LoanId = loanId, Month = m.Month.ToString(), Amount = m.Amount, Paid = m.Paid, UpdatedAt = DateTime.UtcNow,
            });
        }
        entity.PaidInstallments = revised.Count(m => m.Paid);

        await db.SaveChangesAsync(ct);
    }

    public async Task HandleDetailsUpdated(Guid loanId, LoanEvents.V1.LoanDetailsUpdated e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, ct);
        if (entity is null) return;

        entity.Description = e.Description;
        entity.TotalAmount = e.TotalAmount;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleMonthAmountOverridden(Guid loanId, LoanEvents.V1.LoanMonthAmountOverridden e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var month = await db.LoanMonths.FirstOrDefaultAsync(m => m.LoanId == loanId && m.Month == e.Month, ct);
        if (month is null) return;

        month.Amount = e.Amount;
        month.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task HandleMonthPaidToggled(Guid loanId, LoanEvents.V1.LoanMonthPaidToggled e, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var month = await db.LoanMonths.FirstOrDefaultAsync(m => m.LoanId == loanId && m.Month == e.Month, ct);
        if (month is null) return;

        month.Paid = !month.Paid;
        month.UpdatedAt = DateTime.UtcNow;

        // Denormalized counter is always recomputed from the months, never incremented (DOMAIN_MODEL.md §5).
        var loan = await db.Loans.Include(l => l.Months).FirstAsync(l => l.Id == loanId, ct);
        loan.PaidInstallments = loan.Months.Count(m => m.Paid);
        loan.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    public async Task HandleRemoved(Guid loanId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Loans.Where(l => l.Id == loanId).ExecuteDeleteAsync(ct);
    }
}
