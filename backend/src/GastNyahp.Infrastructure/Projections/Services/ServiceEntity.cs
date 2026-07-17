namespace GastNyahp.Infrastructure.Projections.Services;

public class ServiceEntity
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string BillingType { get; set; } = "";
    public Guid? LinkedCardId { get; set; }
    public bool Active { get; set; } = true;
    public string Currency { get; set; } = "";
    public decimal? OriginalAmount { get; set; }
    public string? OriginalCurrency { get; set; }
    public string OwnerKind { get; set; } = "";
    public Guid? OwnerPersonId { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<ServiceMonthAmountEntity> Amounts { get; set; } = [];
}

public class ServiceMonthAmountEntity
{
    public Guid ServiceId { get; set; }
    public string Month { get; set; } = "";
    public decimal AmountArs { get; set; }
    public bool Paid { get; set; }
    public DateTime UpdatedAt { get; set; }

    // EF back-navigation only — never serialized (would cycle parent↔child in API responses).
    [System.Text.Json.Serialization.JsonIgnore]
    public ServiceEntity? Service { get; set; }
}
