namespace GastNyahp.Infrastructure.Projections.Expenses;

public class TicketEntity
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string Date { get; set; } = "";
    public string Description { get; set; } = "";
    public string PaymentMethodKind { get; set; } = "";
    public Guid? PaymentMethodReferenceId { get; set; }
    public decimal Discount { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<TicketItemEntity> Items { get; set; } = [];

    /// <summary>Denormalized Max(0, Sum(Items.Amount) - Discount) — kept in sync by the projection handler on
    /// every TicketRegistered/TicketUpdated so dashboard totals don't need to join+aggregate Items each time.</summary>
    public decimal Total { get; set; }
}

public class TicketItemEntity
{
    public Guid TicketId { get; set; }
    public Guid ItemId { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string Category { get; set; } = "";
    public string OwnerKind { get; set; } = "";
    public Guid? OwnerPersonId { get; set; }

    // EF back-navigation only — never serialized (would cycle parent↔child in API responses).
    [System.Text.Json.Serialization.JsonIgnore]
    public TicketEntity? Ticket { get; set; }
}
