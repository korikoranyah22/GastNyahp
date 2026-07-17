using GastNyahp.Domain.Common;

namespace GastNyahp.Domain.Tests.Common;

public class BillingCycleTests
{
    [Fact]
    public void GetBillingMonth_rolls_to_next_month_when_after_closing_day()
    {
        // closing day 15, purchase on the 20th → belongs to March's statement.
        var billing = BillingCycle.GetBillingMonth(new DateOnly(2026, 2, 20), closingDay: 15);
        Assert.Equal(YearMonth.Parse("2026-03"), billing);
    }

    [Fact]
    public void GetBillingMonth_stays_same_month_when_on_or_before_closing_day()
    {
        var billing = BillingCycle.GetBillingMonth(new DateOnly(2026, 2, 10), closingDay: 15);
        Assert.Equal(YearMonth.Parse("2026-02"), billing);
    }

    [Fact]
    public void GetBillingMonth_rolls_over_year_boundary()
    {
        var billing = BillingCycle.GetBillingMonth(new DateOnly(2026, 12, 20), closingDay: 15);
        Assert.Equal(YearMonth.Parse("2027-01"), billing);
    }

    [Fact]
    public void GetPaymentMonth_is_next_month_when_dueDay_before_closingDay()
    {
        // Typical Argentine case: closes the 15th, due the 5th of the FOLLOWING month.
        var payment = BillingCycle.GetPaymentMonth(new DateOnly(2026, 2, 10), closingDay: 15, dueDay: 5);
        Assert.Equal(YearMonth.Parse("2026-03"), payment);
    }

    [Fact]
    public void GetPaymentMonth_is_same_month_when_dueDay_after_or_equal_closingDay()
    {
        var payment = BillingCycle.GetPaymentMonth(new DateOnly(2026, 2, 10), closingDay: 15, dueDay: 20);
        Assert.Equal(YearMonth.Parse("2026-02"), payment);
    }

    [Fact]
    public void GetPaymentMonth_falls_back_to_billing_month_when_no_dueDay()
    {
        var payment = BillingCycle.GetPaymentMonth(new DateOnly(2026, 2, 20), closingDay: 15, dueDay: null);
        Assert.Equal(YearMonth.Parse("2026-03"), payment);
    }

    [Fact]
    public void GetEffectiveMonth_uses_calendar_month_for_non_card_payments()
    {
        var effective = BillingCycle.GetEffectiveMonth(new DateOnly(2026, 2, 20), cardClosingDay: null, cardDueDay: null);
        Assert.Equal(YearMonth.Parse("2026-02"), effective);
    }

    [Fact]
    public void GetEffectiveMonth_uses_payment_month_for_card_payments()
    {
        // Day 20 > closingDay 15 → billing month rolls to March; dueDay 5 < closingDay 15 → payment rolls
        // one more month, to April (see GetPaymentMonth_is_next_month_when_dueDay_before_closingDay above).
        var effective = BillingCycle.GetEffectiveMonth(new DateOnly(2026, 2, 20), cardClosingDay: 15, cardDueDay: 5);
        Assert.Equal(YearMonth.Parse("2026-04"), effective);
    }
}
