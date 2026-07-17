namespace GastNyahp.Infrastructure.Projections.Installments;

public class InstallmentPurchaseEntity
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public Guid CardId { get; set; }
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string PurchaseDate { get; set; } = "";
    public string Frequency { get; set; } = "";
    public decimal MonthlyAmount { get; set; }
    public int? TotalInstallments { get; set; }
    public string StartMonth { get; set; } = "";
    public string OwnerKind { get; set; } = "";
    public Guid? OwnerPersonId { get; set; }
    public bool Active { get; set; } = true;
    public DateTime UpdatedAt { get; set; }

    public List<InstallmentMonthEntity> Months { get; set; } = [];
}

/// <summary>One cell of an installment's calendar — natural key (InstallmentId, Month).</summary>
public class InstallmentMonthEntity
{
    public Guid InstallmentId { get; set; }
    public string Month { get; set; } = "";
    public decimal Amount { get; set; }
    public bool Paid { get; set; }
    public DateTime UpdatedAt { get; set; }

    // EF back-navigation only — never serialized (would cycle parent↔child in API responses).
    [System.Text.Json.Serialization.JsonIgnore]
    public InstallmentPurchaseEntity? Installment { get; set; }
}
