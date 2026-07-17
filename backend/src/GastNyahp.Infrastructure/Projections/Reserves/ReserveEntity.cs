namespace GastNyahp.Infrastructure.Projections.Reserves;

public class ReserveEntity
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string Label { get; set; } = "";
    public string Type { get; set; } = "";
    public string Icon { get; set; } = "";
    public bool Recurring { get; set; }
    public decimal BaseAmount { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<ReserveMonthOverrideEntity> Months { get; set; } = [];
}

public class ReserveMonthOverrideEntity
{
    public Guid ReserveId { get; set; }
    public string Month { get; set; } = "";
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public DateTime UpdatedAt { get; set; }

    // EF back-navigation only — never serialized (would cycle parent↔child in API responses).
    [System.Text.Json.Serialization.JsonIgnore]
    public ReserveEntity? Reserve { get; set; }
}
