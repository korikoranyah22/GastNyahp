using System.Globalization;

namespace GastNyahp.Domain.Common;

/// <summary>Calendar month, no day component. The natural key for installment/loan/service/reserve months.</summary>
public readonly record struct YearMonth : IComparable<YearMonth>
{
    public int Year { get; }
    public int Month { get; }

    public YearMonth(int year, int month)
    {
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month), "Month must be 1-12.");
        Year = year;
        Month = month;
    }

    public static YearMonth Parse(string value)
    {
        var parts = value.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var y) || !int.TryParse(parts[1], out var m))
            throw new FormatException($"'{value}' is not a valid YearMonth (expected 'yyyy-MM').");
        return new YearMonth(y, m);
    }

    public static YearMonth FromDate(DateOnly date) => new(date.Year, date.Month);

    public YearMonth AddMonths(int count)
    {
        var total = Year * 12 + (Month - 1) + count;
        var y = total / 12;
        var m = total % 12;
        if (m < 0) { m += 12; y -= 1; }
        return new YearMonth(y, m + 1);
    }

    /// <summary>`count` consecutive months starting at this one (inclusive).</summary>
    public IReadOnlyList<YearMonth> Take(int count) =>
        Enumerable.Range(0, count).Select(AddMonths).ToList();

    public int CompareTo(YearMonth other) => (Year * 12 + Month).CompareTo(other.Year * 12 + other.Month);
    public static bool operator <(YearMonth a, YearMonth b) => a.CompareTo(b) < 0;
    public static bool operator >(YearMonth a, YearMonth b) => a.CompareTo(b) > 0;
    public static bool operator <=(YearMonth a, YearMonth b) => a.CompareTo(b) <= 0;
    public static bool operator >=(YearMonth a, YearMonth b) => a.CompareTo(b) >= 0;

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
