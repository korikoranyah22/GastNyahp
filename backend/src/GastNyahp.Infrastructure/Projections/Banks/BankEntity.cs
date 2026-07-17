namespace GastNyahp.Infrastructure.Projections.Banks;

/// <summary>Flat read-model row for the Bank aggregate — no logic, see eventuous-projection-readmodel.</summary>
public class BankEntity
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string Name { get; set; } = "";
    public string? Alias { get; set; }
    public string Color { get; set; } = "";
    public string Icon { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}
