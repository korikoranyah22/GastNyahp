using GastNyahp.Domain.BusinessDays;
using GastNyahp.Infrastructure.EventStore;
using GastNyahp.Infrastructure.Projections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GastNyahp.Infrastructure.Services;

public class BusinessDayService(
    BusinessDayCommandService commands,
    IDbContextFactory<ProjectionsDbContext> dbFactory,
    IReadModelSync sync,
    ILogger<BusinessDayService> logger)
{
    public async Task<bool> IsOpenAsync(string date, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.BusinessDays.AnyAsync(d => d.Date == date, ct);
    }

    /// <summary>BusinessDay is GLOBAL (the app's clock, DOMAIN_MODEL.md §17.3) — idempotent per date.</summary>
    public Task<OpResult> OpenAsync(string date, CancellationToken ct = default) =>
        CommandExecutor.Exec(commands.Handle(new OpenBusinessDay(date), ct), sync, logger, "OpenBusinessDay", null, ct);

    /// <summary>"Novedades del día" for ONE family — derived query, nothing persisted (§13.2).</summary>
    public async Task<DayNovelties> GetNoveltiesAsync(Guid familyId, string date, CancellationToken ct = default)
    {
        var month = date[..7];
        var day = int.Parse(date[8..]);

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var installments = await (
            from m in db.InstallmentMonths
            where m.Month == month && !m.Paid
            join i in db.InstallmentPurchases.Where(i => i.Active && i.FamilyId == familyId) on m.InstallmentId equals i.Id
            join c in db.CreditCards on i.CardId equals c.Id
            select new PendingItem(i.Description, m.Amount, c.Label)).ToListAsync(ct);

        var loans = await (
            from m in db.LoanMonths
            where m.Month == month && !m.Paid
            join l in db.Loans.Where(l => l.FamilyId == familyId) on m.LoanId equals l.Id
            select new PendingItem(l.Description, m.Amount, null)).ToListAsync(ct);

        var services = await (
            from a in db.ServiceMonthAmounts
            where a.Month == month && !a.Paid && a.AmountArs > 0
            join s in db.Services.Where(s => s.Active && s.FamilyId == familyId) on a.ServiceId equals s.Id
            select new PendingItem(s.Name, a.AmountArs, null)).ToListAsync(ct);

        var closingToday = await db.CreditCards.Where(c => c.FamilyId == familyId && c.Active && c.ClosingDay == day).Select(c => c.Label).ToListAsync(ct);
        var dueToday = await db.CreditCards.Where(c => c.FamilyId == familyId && c.Active && c.DueDay == day).Select(c => c.Label).ToListAsync(ct);

        var openDrafts = await db.Drafts.CountAsync(d => d.FamilyId == familyId && d.Status == "Open", ct);

        return new DayNovelties(date, installments, loans, services, closingToday, dueToday, openDrafts);
    }
}

public record PendingItem(string Description, decimal Amount, string? CardLabel);

public record DayNovelties(
    string Date,
    IReadOnlyList<PendingItem> UnpaidInstallments,
    IReadOnlyList<PendingItem> UnpaidLoanMonths,
    IReadOnlyList<PendingItem> UnpaidServices,
    IReadOnlyList<string> CardsClosingToday,
    IReadOnlyList<string> CardsDueToday,
    int OpenDrafts);
