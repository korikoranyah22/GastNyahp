namespace GastNyahp.Infrastructure.Projections.People;

public class PersonEntity
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string Name { get; set; } = "";
    public string Emoji { get; set; } = "";
    public string Color { get; set; } = "";
    public bool Archived { get; set; }
    public DateTime UpdatedAt { get; set; }
}
