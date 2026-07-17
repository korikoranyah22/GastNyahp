using Eventuous;
using GastNyahp.Domain.BusinessDays;

namespace GastNyahp.Domain.Tests.BusinessDays;

public class BusinessDayTests
{
    [Fact]
    public void Open_produces_event_with_the_given_date()
    {
        var e = (BusinessDayEvents.V1.BusinessDayOpened)BusinessDayCommandService.Open(new OpenBusinessDay("2026-07-09")).Single();
        Assert.Equal("2026-07-09", e.Date);
        Assert.False(string.IsNullOrEmpty(e.OpenedAt));
    }

    [Fact]
    public void Open_without_date_throws() =>
        Assert.Throws<DomainException>(() => BusinessDayCommandService.Open(new OpenBusinessDay("")).ToList());

    // Re-opening the SAME date is rejected at the CommandService.Handle level via ExpectedState.New — see
    // eventuous-event-sourced-aggregate and DOMAIN_MODEL.md §13.1: the guard lives in stream-loading, not in
    // the static Open() handler itself, so it can't be exercised by calling Open() twice in isolation here.
    // It's covered by an integration test once the application-service layer exists.
}
