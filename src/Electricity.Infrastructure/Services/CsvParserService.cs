using System.Globalization;
using System.Text;
using CsvHelper;
using Electricity.Application.DTOs;
using Electricity.Application.Interfaces;
using Electricity.Infrastructure.Configuration;
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
            var config = CsvParserConfiguration.CreateDefaultConfiguration(
                rawRecord => _logger.LogWarning("Bad data found: {RawRecord}", rawRecord));

            using var reader = new StreamReader(csvStream, Encoding.UTF8);
            using var csv = new CsvReader(reader, config);

            await csv.ReadAsync();
            csv.ReadHeader();

            var header = csv.HeaderRecord;
            ValidateHeader(header);

            if (header is null)
            {
                throw new InvalidOperationException("CSV header is null after validation");
            }

            _logger.LogInformation("CSV header columns: {Columns}", string.Join(", ", header));

            var hourlyColumnIndices = GetHourlyColumnIndices(header);
            int rowNumber = 1;

            while (await csv.ReadAsync())
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var record = ParseRow(csv, hourlyColumnIndices, rowNumber);
                    if (record is not null)
                    {
                        results.Add(record);
                    }

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

    private void ValidateHeader(string[]? header)
    {
        if (header is null || header.Length < CsvParserConfiguration.MinimumHeaderColumns)
        {
            throw new InvalidOperationException("CSV file has invalid or missing header");
        }
    }

    private static List<int> GetHourlyColumnIndices(string[] header)
    {
        var hourlyColumnIndices = new List<int>();
        for (int i = CsvParserConfiguration.FirstDataColumnIndex; i < header.Length; i++)
        {
            hourlyColumnIndices.Add(i);
        }
        return hourlyColumnIndices;
    }

    private RawElectricityDataDto? ParseRow(CsvReader csv, List<int> hourlyColumnIndices, int rowNumber)
    {
        var tinklas = csv.GetField<string>(0)?.Trim() ?? string.Empty;
        var objektoTipas = csv.GetField<string>(1)?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(tinklas) || string.IsNullOrEmpty(objektoTipas))
        {
            _logger.LogWarning("Skipping row {Row} due to missing Tinklas or ObjektoTipas", rowNumber);
            return null;
        }

        var hourlyConsumption = ParseHourlyConsumption(csv, hourlyColumnIndices);

        return new RawElectricityDataDto
        {
            Tinklas = tinklas,
            ObjektoTipas = objektoTipas,
            HourlyConsumption = hourlyConsumption
        };
    }

    private static Dictionary<int, decimal> ParseHourlyConsumption(CsvReader csv, List<int> hourlyColumnIndices)
    {
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

        return hourlyConsumption;
    }
}
