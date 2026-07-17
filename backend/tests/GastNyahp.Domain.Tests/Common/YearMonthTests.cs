using GastNyahp.Domain.Common;

namespace GastNyahp.Domain.Tests.Common;

public class YearMonthTests
{
    [Fact]
    public void Parse_and_ToString_roundtrip() =>
        Assert.Equal("2026-02", YearMonth.Parse("2026-02").ToString());

    [Theory]
    [InlineData(2026, 2, 1, 2026, 3)]
    [InlineData(2026, 12, 1, 2027, 1)]
    [InlineData(2026, 1, -1, 2025, 12)]
    public void AddMonths_handles_year_rollover(int y, int m, int add, int expY, int expM)
    {
        var result = new YearMonth(y, m).AddMonths(add);
        Assert.Equal(new YearMonth(expY, expM), result);
    }

    [Fact]
    public void Take_generates_consecutive_months()
    {
        var months = YearMonth.Parse("2025-11").Take(3);
        Assert.Equal(["2025-11", "2025-12", "2026-01"], months.Select(m => m.ToString()));
    }

    [Fact]
    public void Comparison_operators_order_by_calendar_time()
    {
        Assert.True(YearMonth.Parse("2026-01") < YearMonth.Parse("2026-02"));
        Assert.True(YearMonth.Parse("2026-12") < YearMonth.Parse("2027-01"));
    }
}
