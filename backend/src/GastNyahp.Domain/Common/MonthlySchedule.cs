namespace GastNyahp.Domain.Common;

/// <summary>One cell of an installment/loan calendar.</summary>
public readonly record struct ScheduleMonth(YearMonth Month, decimal Amount, bool Paid);

/// <summary>
/// Shared calendar generation/regeneration algorithm for InstallmentPurchase and Loan (DOMAIN_MODEL.md §4/§5).
/// Ported 1:1 from the frontend's addInstallment/updateInstallment regeneration rule, and deliberately reused
/// for Loan too (the frontend's updateLoan never regenerates — that asymmetry is a bug, not a rule; see §5).
/// </summary>
public static class MonthlySchedule
{
    public static IReadOnlyList<ScheduleMonth> Generate(YearMonth start, int count, decimal amount) =>
        start.Take(count).Select(m => new ScheduleMonth(m, amount, Paid: false)).ToList();

    /// <summary>
    /// Regenerates the calendar for new (start, count, amount), preserving Paid=true months' Paid flag AND
    /// their existing (possibly overridden) amount — a month that was already marked paid never has its
    /// historical amount silently rewritten by a later schedule revision.
    /// </summary>
    public static IReadOnlyList<ScheduleMonth> Revise(
        IReadOnlyList<ScheduleMonth> existing, YearMonth newStart, int newCount, decimal newAmount)
    {
        var paid = existing.Where(m => m.Paid).ToDictionary(m => m.Month, m => m.Amount);

        return newStart.Take(newCount)
            .Select(m => paid.TryGetValue(m, out var preservedAmount)
                ? new ScheduleMonth(m, preservedAmount, Paid: true)
                : new ScheduleMonth(m, newAmount, Paid: false))
            .ToList();
    }

    public static IReadOnlyList<ScheduleMonth> TogglePaid(IReadOnlyList<ScheduleMonth> months, YearMonth month) =>
        months.Select(m => m.Month == month ? m with { Paid = !m.Paid } : m).ToList();

    public static IReadOnlyList<ScheduleMonth> OverrideAmount(IReadOnlyList<ScheduleMonth> months, YearMonth month, decimal amount) =>
        months.Select(m => m.Month == month ? m with { Amount = amount } : m).ToList();
}
