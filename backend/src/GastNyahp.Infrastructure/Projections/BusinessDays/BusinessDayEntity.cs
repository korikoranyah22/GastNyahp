namespace GastNyahp.Infrastructure.Projections.BusinessDays;

/// <summary>Natural key = Date ("yyyy-MM-dd"), mirrors the BusinessDay aggregate's stream key.</summary>
public class BusinessDayEntity
{
    public string Date { get; set; } = "";
    public DateTime OpenedAt { get; set; }
}
