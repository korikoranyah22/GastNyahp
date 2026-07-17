namespace GastNyahp.Infrastructure.Projections.Loans;

public class LoanEntity
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public Guid BankId { get; set; }
    public string Description { get; set; } = "";
    public decimal? TotalAmount { get; set; }
    public decimal MonthlyInstallment { get; set; }
    public string StartMonth { get; set; } = "";
    public int TotalInstallments { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<LoanMonthEntity> Months { get; set; } = [];

    /// <summary>Denormalized for cheap dashboard reads — always recomputable from Months, never hand-mutated
    /// independently (see DOMAIN_MODEL.md §5 decision on PaidInstallments being a derived value).</summary>
    public int PaidInstallments { get; set; }
}

public class LoanMonthEntity
{
    public Guid LoanId { get; set; }
    public string Month { get; set; } = "";
    public decimal Amount { get; set; }
    public bool Paid { get; set; }
    public DateTime UpdatedAt { get; set; }

    // EF back-navigation only — never serialized (would cycle parent↔child in API responses).
    [System.Text.Json.Serialization.JsonIgnore]
    public LoanEntity? Loan { get; set; }
}
