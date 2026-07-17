namespace GastNyahp.Infrastructure.Projections.Drafts;

/// <summary>
/// Read model de borradores. El payload va como JSON crudo (columna PayloadJson): un borrador es un documento
/// parcial por definición y nadie filtra por sus campos internos — se lista por familia/estado y se muestra.
/// </summary>
public class DraftEntity
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string Kind { get; set; } = "";        // Expense | Ticket | Installment
    public string Status { get; set; } = "";      // Open | Confirmed | Discarded
    public string PayloadJson { get; set; } = "{}";
    public string CreatedByKind { get; set; } = ""; // Member | Agent
    public Guid CreatedById { get; set; }
    public Guid? ResultEntityId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
