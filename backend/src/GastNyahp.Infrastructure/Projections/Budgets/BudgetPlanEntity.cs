namespace GastNyahp.Infrastructure.Projections.Budgets;

/// <summary>Natural key = (FamilyId, Month) — one budget per family per month (DOMAIN_MODEL.md §17.3).</summary>
public class BudgetPlanEntity
{
    public Guid FamilyId { get; set; }
    public string Month { get; set; } = "";
    public decimal CreditLimit { get; set; }
    public decimal DebitCashLimit { get; set; }
    public decimal WeeklyLimit { get; set; }
    public DateTime UpdatedAt { get; set; }
}
