using GastNyahp.Infrastructure;

namespace GastNyahp.Integration.Tests;

public class BusinessDaySchedulerTests
{
    static readonly TimeOnly OpenTime = new(6, 0);

    [Fact]
    public void NextDelay_targets_today_when_open_time_is_still_ahead()
    {
        var now = new DateTimeOffset(2026, 7, 9, 3, 30, 0, TimeSpan.FromHours(-3));
        Assert.Equal(TimeSpan.FromHours(2.5), BusinessDayScheduler.NextDelay(now, OpenTime));
    }

    [Fact]
    public void NextDelay_targets_tomorrow_when_open_time_already_passed()
    {
        var now = new DateTimeOffset(2026, 7, 9, 15, 0, 0, TimeSpan.FromHours(-3));
        Assert.Equal(TimeSpan.FromHours(15), BusinessDayScheduler.NextDelay(now, OpenTime));
    }

    [Fact]
    public void NextDelay_exactly_at_open_time_waits_a_full_day()
    {
        // At exactly 06:00 the current iteration already opened today's day — next opening is tomorrow's.
        var now = new DateTimeOffset(2026, 7, 9, 6, 0, 0, TimeSpan.FromHours(-3));
        Assert.Equal(TimeSpan.FromDays(1), BusinessDayScheduler.NextDelay(now, OpenTime));
    }
}
