namespace GastNyahp.Infrastructure.Projections.Income;

/// <summary>One singleton row PER FAMILY (DOMAIN_MODEL.md §12/§17.3).</summary>
public class IncomeEntity
{
    public Guid FamilyId { get; set; }
    public decimal NetMonthly { get; set; }
    public decimal UsdRateOfficial { get; set; }
    public decimal UsdRateCcl { get; set; }
    public int SplitPercent { get; set; } = 70;
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Append-only — one row per IncomeUpdated event, so "what was the net income in month X" can be
/// answered without modeling Income as a per-month aggregate. See DOMAIN_MODEL.md §12.</summary>
public class IncomeHistoryEntity
{
    public long SequenceNumber { get; set; }
    public Guid FamilyId { get; set; }
    public DateTime ChangedAt { get; set; }
    public decimal NetMonthly { get; set; }
    public decimal UsdRateOfficial { get; set; }
    public decimal UsdRateCcl { get; set; }
    public int SplitPercent { get; set; }
}
