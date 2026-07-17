namespace GastNyahp.Domain.Common;

/// <summary>
/// Credit card billing-cycle math. Ported 1:1 from app/src/pages/expenses/expensesConfig.js
/// (getBillingMonth / getPaymentMonth) — see DOMAIN_MODEL.md §3.
/// </summary>
public static class BillingCycle
{
    /// <summary>The closing-statement month a purchase falls into. If the purchase day is after the
    /// closing day, it rolls into next month's statement.</summary>
    public static YearMonth GetBillingMonth(DateOnly purchaseDate, int closingDay)
    {
        var ym = YearMonth.FromDate(purchaseDate);
        return purchaseDate.Day > closingDay ? ym.AddMonths(1) : ym;
    }

    /// <summary>The month in which the statement is actually paid. If dueDay &lt; closingDay, the due date
    /// falls the month AFTER the closing month (the typical Argentine case). Otherwise it's the same month.</summary>
    public static YearMonth GetPaymentMonth(DateOnly purchaseDate, int closingDay, int? dueDay)
    {
        var billing = GetBillingMonth(purchaseDate, closingDay);
        if (dueDay is null || dueDay >= closingDay) return billing;
        return billing.AddMonths(1);
    }

    /// <summary>The month an expense counts toward: payment month for credit-card expenses, calendar
    /// month for anything else (debit/cash/digital wallets).</summary>
    public static YearMonth GetEffectiveMonth(DateOnly expenseDate, int? cardClosingDay, int? cardDueDay) =>
        cardClosingDay is > 0
            ? GetPaymentMonth(expenseDate, cardClosingDay.Value, cardDueDay)
            : YearMonth.FromDate(expenseDate);
}
