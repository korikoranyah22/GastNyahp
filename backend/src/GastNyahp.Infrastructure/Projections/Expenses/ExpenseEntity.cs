namespace GastNyahp.Infrastructure.Projections.Expenses;

public class ExpenseEntity
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string Date { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal AmountArs { get; set; }
    public decimal? OriginalAmount { get; set; }
    public string? OriginalCurrency { get; set; }
    public string PaymentMethodKind { get; set; } = "";
    public Guid? PaymentMethodReferenceId { get; set; }
    public string OwnerKind { get; set; } = "";
    public Guid? OwnerPersonId { get; set; }
    public DateTime UpdatedAt { get; set; }
}
