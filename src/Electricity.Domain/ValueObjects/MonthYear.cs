namespace Electricity.Domain.ValueObjects;

public readonly record struct MonthYear
{
    public int Year { get; }
    public int Month { get; }

    public MonthYear(int year, int month)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 2020, nameof(year));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 2100, nameof(year));
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1, nameof(month));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12, nameof(month));

        Year = year;
        Month = month;
    }

    public string ToFileName() => $"{Year}-{Month:D2}.csv";

    public DateTime ToDateTime() => new(Year, Month, 1, 0, 0, 0, DateTimeKind.Utc);

    public override string ToString() => $"{Year}-{Month:D2}";
}
