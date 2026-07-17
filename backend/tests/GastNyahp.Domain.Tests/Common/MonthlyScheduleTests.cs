using GastNyahp.Domain.Common;

namespace GastNyahp.Domain.Tests.Common;

public class MonthlyScheduleTests
{
    [Fact]
    public void Generate_creates_count_months_all_unpaid()
    {
        var months = MonthlySchedule.Generate(YearMonth.Parse("2025-10"), 3, 85000m);

        Assert.Equal(3, months.Count);
        Assert.All(months, m => Assert.False(m.Paid));
        Assert.Equal(["2025-10", "2025-11", "2025-12"], months.Select(m => m.Month.ToString()));
    }

    [Fact]
    public void Revise_preserves_paid_flag_and_amount_of_months_that_were_already_paid()
    {
        var original = MonthlySchedule.Generate(YearMonth.Parse("2025-10"), 4, 85000m);
        // Mark Oct and Nov as paid, with Nov's amount overridden before the revision (e.g. a partial payment).
        original = MonthlySchedule.TogglePaid(original, YearMonth.Parse("2025-10"));
        original = MonthlySchedule.TogglePaid(original, YearMonth.Parse("2025-11"));
        original = MonthlySchedule.OverrideAmount(original, YearMonth.Parse("2025-11"), 90000m);

        // Revise to a longer schedule with a new base amount.
        var revised = MonthlySchedule.Revise(original, YearMonth.Parse("2025-10"), 6, 100000m);

        Assert.Equal(6, revised.Count);
        var oct = revised.Single(m => m.Month == YearMonth.Parse("2025-10"));
        var nov = revised.Single(m => m.Month == YearMonth.Parse("2025-11"));
        var dec = revised.Single(m => m.Month == YearMonth.Parse("2025-12"));

        Assert.True(oct.Paid); Assert.Equal(85000m, oct.Amount);   // untouched paid month keeps its historical amount
        Assert.True(nov.Paid); Assert.Equal(90000m, nov.Amount);   // preserves the earlier per-month override too
        Assert.False(dec.Paid); Assert.Equal(100000m, dec.Amount); // unpaid months pick up the new base amount
    }

    [Fact]
    public void Revise_drops_paid_months_that_fall_outside_the_new_window()
    {
        var original = MonthlySchedule.Generate(YearMonth.Parse("2025-10"), 2, 85000m);
        original = MonthlySchedule.TogglePaid(original, YearMonth.Parse("2025-10"));

        // New schedule starts AFTER the previously-paid month — it's simply not part of the new calendar.
        var revised = MonthlySchedule.Revise(original, YearMonth.Parse("2025-12"), 3, 85000m);

        Assert.DoesNotContain(revised, m => m.Month == YearMonth.Parse("2025-10"));
        Assert.Equal(3, revised.Count);
    }

    [Fact]
    public void TogglePaid_flips_only_the_targeted_month()
    {
        var months = MonthlySchedule.Generate(YearMonth.Parse("2026-01"), 2, 1000m);
        var toggled = MonthlySchedule.TogglePaid(months, YearMonth.Parse("2026-01"));

        Assert.True(toggled.Single(m => m.Month == YearMonth.Parse("2026-01")).Paid);
        Assert.False(toggled.Single(m => m.Month == YearMonth.Parse("2026-02")).Paid);

        var toggledBack = MonthlySchedule.TogglePaid(toggled, YearMonth.Parse("2026-01"));
        Assert.False(toggledBack.Single(m => m.Month == YearMonth.Parse("2026-01")).Paid);
    }

    [Fact]
    public void OverrideAmount_changes_only_the_targeted_month()
    {
        var months = MonthlySchedule.Generate(YearMonth.Parse("2026-01"), 2, 1000m);
        var overridden = MonthlySchedule.OverrideAmount(months, YearMonth.Parse("2026-02"), 500m);

        Assert.Equal(1000m, overridden.Single(m => m.Month == YearMonth.Parse("2026-01")).Amount);
        Assert.Equal(500m, overridden.Single(m => m.Month == YearMonth.Parse("2026-02")).Amount);
    }
}
