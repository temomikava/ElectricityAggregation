using System.Globalization;
using CsvHelper.Configuration;

namespace Electricity.Infrastructure.Configuration;

public static class CsvParserConfiguration
{
    public const string Delimiter = ";";
    public const int MinimumHeaderColumns = 3;
    public const int FirstDataColumnIndex = 2;

    public static CsvConfiguration CreateDefaultConfiguration(Action<string>? badDataHandler = null)
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = Delimiter,
            HasHeaderRecord = true,
            BadDataFound = badDataHandler is not null
                ? context => badDataHandler(context.RawRecord)
                : null,
            MissingFieldFound = null,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim
        };
    }
}
