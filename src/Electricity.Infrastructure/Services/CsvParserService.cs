using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Electricity.Application.DTOs;
using Electricity.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Electricity.Infrastructure.Services;

public class CsvParserService : ICsvParser
{
    private readonly ILogger<CsvParserService> _logger;

    public CsvParserService(ILogger<CsvParserService> logger)
    {
        _logger = logger;
    }

    public async Task<List<RawElectricityDataDto>> ParseCsvAsync(Stream csvStream, CancellationToken cancellationToken = default)
    {
        var results = new List<RawElectricityDataDto>();

        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true,
                BadDataFound = context =>
                {
                    _logger.LogWarning("Bad data found: {RawRecord}",
                        context.RawRecord);
                },
                MissingFieldFound = null,
                IgnoreBlankLines = true,
                TrimOptions = TrimOptions.Trim
            };

            using var reader = new StreamReader(csvStream, Encoding.UTF8);
            using var csv = new CsvReader(reader, config);

            await csv.ReadAsync();
            csv.ReadHeader();

            var header = csv.HeaderRecord;
            if (header == null || header.Length < 3)
            {
                throw new InvalidOperationException("CSV file has invalid or missing header");
            }

            _logger.LogInformation("CSV header columns: {Columns}", string.Join(", ", header));

            var hourlyColumnIndices = new List<int>();
            for (int i = 2; i < header.Length; i++)
            {
                hourlyColumnIndices.Add(i);
            }

            int rowNumber = 1;
            while (await csv.ReadAsync())
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var tinklas = csv.GetField<string>(0)?.Trim() ?? string.Empty;
                    var objektoTipas = csv.GetField<string>(1)?.Trim() ?? string.Empty;

                    if (string.IsNullOrEmpty(tinklas) || string.IsNullOrEmpty(objektoTipas))
                    {
                        _logger.LogWarning("Skipping row {Row} due to missing Tinklas or ObjektoTipas", rowNumber);
                        rowNumber++;
                        continue;
                    }

                    var hourlyConsumption = new Dictionary<int, decimal>();

                    for (int i = 0; i < hourlyColumnIndices.Count; i++)
                    {
                        var columnIndex = hourlyColumnIndices[i];
                        var valueStr = csv.GetField<string>(columnIndex);

                        if (!string.IsNullOrWhiteSpace(valueStr))
                        {
                            valueStr = valueStr.Replace(",", ".");

                            if (decimal.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                            {
                                hourlyConsumption[i] = value;
                            }
                        }
                    }

                    results.Add(new RawElectricityDataDto
                    {
                        Tinklas = tinklas,
                        ObjektoTipas = objektoTipas,
                        HourlyConsumption = hourlyConsumption
                    });

                    rowNumber++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing row {Row}, skipping", rowNumber);
                    rowNumber++;
                }
            }

            _logger.LogInformation("Successfully parsed {Count} records from CSV", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse CSV file");
            throw;
        }
    }
}
