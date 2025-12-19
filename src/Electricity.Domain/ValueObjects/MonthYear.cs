namespace Electricity.Domain.ValueObjects;

public record MonthYear
{
    public int Year { get; init; }
    public int Month { get; init; }

    public MonthYear(int year, int month)
    {
        if (year < 2020 || year > DateTime.Now.Year)
            throw new ArgumentException("Invalid year", nameof(year));
        if (month < 1 || month > 12)
            throw new ArgumentException("Invalid month", nameof(month));

        Year = year;
        Month = month;
    }

    public string ToFileName() => $"{Year}-{Month:D2}.csv";

    public DateTime ToDateTime() => new DateTime(Year, Month, 1, 0, 0, 0, DateTimeKind.Utc);

    public override string ToString() => $"{Year}-{Month:D2}";
}
