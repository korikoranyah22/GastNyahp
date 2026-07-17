namespace GastNyahp.Infrastructure.Projections.Cards;

public class CreditCardEntity
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public Guid BankId { get; set; }
    public string Label { get; set; } = "";
    public string Network { get; set; } = "";
    public string Type { get; set; } = "";
    public int ClosingDay { get; set; }
    public int DueDay { get; set; }
    public string Color { get; set; } = "";
    public bool Active { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}
