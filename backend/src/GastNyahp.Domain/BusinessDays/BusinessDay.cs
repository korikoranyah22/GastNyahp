using Eventuous;

namespace GastNyahp.Domain.BusinessDays;

public static class BusinessDayEvents
{
    public static class V1
    {
        [EventType("V1.BusinessDayOpened")]
        public record BusinessDayOpened(string Date, string OpenedAt);
    }
}

public record BusinessDayState : State<BusinessDayState>
{
    public string Date { get; init; } = "";
    public string OpenedAt { get; init; } = "";

    public BusinessDayState()
    {
        On<BusinessDayEvents.V1.BusinessDayOpened>((s, e) => s with { Date = e.Date, OpenedAt = e.OpenedAt });
    }
}

public record OpenBusinessDay(string Date);

/// <summary>
/// One stream per calendar date ("business-day-{date}"). ExpectedState.New makes OpenBusinessDay idempotent
/// per date — if the daily IHostedService runs twice for the same date (container restart), the second
/// attempt fails this guard and is logged/ignored, not retried. See DOMAIN_MODEL.md §13.
/// </summary>
public sealed class BusinessDayCommandService : CommandService<BusinessDayState>
{
    public BusinessDayCommandService(IEventStore store) : base(store)
    {
        On<OpenBusinessDay>().InState(ExpectedState.New)
            .GetStream(cmd => Stream(cmd.Date)).Act(Open);
    }

    public static IEnumerable<object> Open(OpenBusinessDay cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd.Date)) throw new DomainException("OpenBusinessDay: Date required.");
        yield return new BusinessDayEvents.V1.BusinessDayOpened(cmd.Date, Now);
    }

    static StreamName Stream(string date) => new($"business-day-{date}");
    static string Now => DateTime.UtcNow.ToString("O");
}
