using System.Diagnostics;
using Electricity.Application.DTOs;
using Electricity.Application.Interfaces;
using Electricity.Domain.Entities;
using Electricity.Domain.Enums;
using Electricity.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Electricity.Application.Services;

public class ElectricityDataService : IElectricityDataService
{
    private readonly IDataSourceRepository _dataSourceRepository;
    private readonly ICsvParser _csvParser;
    private readonly IConsumptionRepository _consumptionRepository;
    private readonly IProcessingLogRepository _processingLogRepository;
    private readonly ILogger<ElectricityDataService> _logger;

    public ElectricityDataService(
        IDataSourceRepository dataSourceRepository,
        ICsvParser csvParser,
        IConsumptionRepository consumptionRepository,
        IProcessingLogRepository processingLogRepository,
        ILogger<ElectricityDataService> logger)
    {
        _dataSourceRepository = dataSourceRepository;
        _csvParser = csvParser;
        _consumptionRepository = consumptionRepository;
        _processingLogRepository = processingLogRepository;
        _logger = logger;
    }

    public async Task<ProcessingResultDto> ProcessMonthDataAsync(MonthYear monthYear, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var monthString = monthYear.ToString();
        var fileName = monthYear.ToFileName();

        _logger.LogInformation("Starting processing for month {Month}", monthString);

        var log = await _processingLogRepository.CreateLogAsync(monthString);

        try
        {
            var csvStream = await DownloadFileAsync(log, fileName, cancellationToken);
            if (csvStream is null)
            {
                return CreateFailureResult(monthString, "Failed to download CSV file", stopwatch.Elapsed);
            }

            var rawData = await ParseCsvAsync(log, csvStream, fileName, cancellationToken);
            if (rawData is null)
            {
                return CreateFailureResult(monthString, "Failed to parse CSV file", stopwatch.Elapsed);
            }

            var apartmentData = FilterApartmentData(rawData, fileName);
            var aggregatedRecords = AggregateDataByRegion(apartmentData, monthYear);

            await UpdateLogStatistics(log, rawData.Count, apartmentData.Count, aggregatedRecords.Count);

            await SaveAggregatedDataAsync(log, monthYear, aggregatedRecords, cancellationToken);

            await CompleteProcessingLog(log);

            stopwatch.Stop();
            _logger.LogInformation("Completed processing for month {Month} in {Duration}ms",
                monthString, stopwatch.ElapsedMilliseconds);

            return CreateSuccessResult(monthString, rawData.Count, apartmentData.Count, aggregatedRecords.Count, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            return await HandleProcessingErrorAsync(log, monthString, ex, stopwatch.Elapsed);
        }
    }

    public async Task<List<RegionConsumptionDto>> GetConsumptionByRegionAsync(DateTime? fromMonth = null, DateTime? toMonth = null)
    {
        DateTime from;
        DateTime to;

        // If no parameters provided, use intelligent defaults based on available data
        if (!fromMonth.HasValue || !toMonth.HasValue)
        {
            var latestMonth = await _consumptionRepository.GetLatestMonthAsync();

            if (latestMonth.HasValue)
            {
                // Use the latest available month and 1 month before it as defaults
                to = toMonth ?? latestMonth.Value;
                from = fromMonth ?? latestMonth.Value.AddMonths(-1);

                _logger.LogInformation(
                    "Using defaults based on latest available data: from {From:yyyy-MM} to {To:yyyy-MM}",
                    from, to);
            }
            else
            {
                // Fallback to current date if no data exists
                to = toMonth ?? DateTime.UtcNow;
                from = fromMonth ?? DateTime.UtcNow.AddMonths(-1);

                _logger.LogWarning(
                    "No data in database, using fallback defaults: from {From:yyyy-MM} to {To:yyyy-MM}",
                    from, to);
            }
        }
        else
        {
            from = fromMonth.Value;
            to = toMonth.Value;
        }

        _logger.LogInformation("Retrieving consumption data from {From:yyyy-MM} to {To:yyyy-MM}", from, to);

        var records = await _consumptionRepository.GetByMonthRangeAsync(from, to);

        if (records.Count == 0)
        {
            _logger.LogInformation("No consumption records found for the specified date range");
            return new List<RegionConsumptionDto>();
        }

        var result = records
            .GroupBy(r => new { r.Region, r.Month })
            .Select(g => new RegionConsumptionDto
            {
                Region = g.Key.Region,
                Month = g.Key.Month,
                TotalConsumption = g.Sum(r => r.TotalConsumption),
                ApartmentCount = g.Sum(r => r.RecordCount),
                AverageConsumption = g.Sum(r => r.RecordCount) > 0
                    ? g.Sum(r => r.TotalConsumption) / g.Sum(r => r.RecordCount)
                    : 0
            })
            .OrderBy(r => r.Month)
            .ThenBy(r => r.Region)
            .ToList();

        _logger.LogInformation("Retrieved {Count} consumption records across {Regions} regions and {Months} months",
            result.Count,
            result.Select(r => r.Region).Distinct().Count(),
            result.Select(r => r.Month).Distinct().Count());

        return result;
    }

    public async Task<List<DataProcessingLogDto>> GetProcessingHistoryAsync(int take = 10)
    {
        _logger.LogInformation("Retrieving {Count} recent processing logs", take);

        var logs = await _processingLogRepository.GetRecentLogsAsync(take);

        return logs.Select(l => new DataProcessingLogDto
        {
            Id = l.Id,
            StartedAt = l.StartedAt,
            CompletedAt = l.CompletedAt,
            Month = l.Month,
            Status = l.Status.ToString(),
            ErrorMessage = l.ErrorMessage,
            RecordsProcessed = l.RecordsProcessed
        }).ToList();
    }

    private async Task<Stream?> DownloadFileAsync(DataProcessingLog log, string fileName, CancellationToken cancellationToken)
    {
        try
        {
            log.Status = ProcessingStatus.Downloading;
            await _processingLogRepository.UpdateLogAsync(log);

            _logger.LogInformation("Downloading CSV file {FileName}", fileName);
            return await _dataSourceRepository.DownloadCsvFileAsync(fileName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download CSV file {FileName}", fileName);
            log.Status = ProcessingStatus.Failed;
            log.ErrorMessage = $"Failed to download CSV: {ex.Message}";
            log.CompletedAt = DateTime.UtcNow;
            await _processingLogRepository.UpdateLogAsync(log);
            return null;
        }
    }

    private async Task<List<RawElectricityDataDto>?> ParseCsvAsync(
        DataProcessingLog log,
        Stream csvStream,
        string fileName,
        CancellationToken cancellationToken)
    {
        try
        {
            log.Status = ProcessingStatus.Parsing;
            await _processingLogRepository.UpdateLogAsync(log);

            _logger.LogInformation("Parsing CSV file {FileName}", fileName);
            await using (csvStream)
            {
                return await _csvParser.ParseCsvAsync(csvStream, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse CSV file {FileName}", fileName);
            log.Status = ProcessingStatus.Failed;
            log.ErrorMessage = $"Failed to parse CSV: {ex.Message}";
            log.CompletedAt = DateTime.UtcNow;
            await _processingLogRepository.UpdateLogAsync(log);
            return null;
        }
    }

    private List<RawElectricityDataDto> FilterApartmentData(List<RawElectricityDataDto> rawData, string fileName)
    {
        _logger.LogInformation("Parsed {Count} records from {FileName}", rawData.Count, fileName);

        var apartmentData = rawData
            .Where(r => r.ObjektoTipas.Equals("Butas", StringComparison.OrdinalIgnoreCase))
            .ToList();

        _logger.LogInformation("Filtered to {FilteredCount} apartment records from {TotalCount} total records",
            apartmentData.Count, rawData.Count);

        return apartmentData;
    }

    private List<ElectricityConsumptionRecord> AggregateDataByRegion(List<RawElectricityDataDto> apartmentData, MonthYear monthYear)
    {
        var aggregatedRecords = apartmentData
            .GroupBy(r => r.Tinklas)
            .Select(g => new ElectricityConsumptionRecord
            {
                Id = Guid.NewGuid(),
                Region = g.Key,
                BuildingType = "Butas",
                Month = monthYear.ToDateTime(),
                TotalConsumption = g.Sum(r => r.HourlyConsumption.Values.Sum()),
                RecordCount = g.Count(),
                ProcessedAt = DateTime.UtcNow,
                SourceFile = monthYear.ToFileName()
            })
            .ToList();

        _logger.LogInformation("Aggregated data into {RegionCount} regions", aggregatedRecords.Count);

        return aggregatedRecords;
    }

    private async Task UpdateLogStatistics(DataProcessingLog log, int totalRecords, int filteredCount, int regionCount)
    {
        log.RecordsProcessed = totalRecords;
        log.RecordsFiltered = filteredCount;
        log.Status = ProcessingStatus.Aggregating;
        await _processingLogRepository.UpdateLogAsync(log);
    }

    private async Task SaveAggregatedDataAsync(
        DataProcessingLog log,
        MonthYear monthYear,
        List<ElectricityConsumptionRecord> aggregatedRecords,
        CancellationToken cancellationToken)
    {
        var monthDateTime = monthYear.ToDateTime();
        var monthString = monthYear.ToString();

        if (await _consumptionRepository.MonthExistsAsync(monthDateTime))
        {
            _logger.LogInformation("Month {Month} already exists, deleting old data", monthString);
            await _consumptionRepository.DeleteByMonthAsync(monthDateTime);
        }

        log.Status = ProcessingStatus.Saving;
        await _processingLogRepository.UpdateLogAsync(log);

        _logger.LogInformation("Saving {Count} consumption records to database", aggregatedRecords.Count);
        await _consumptionRepository.AddConsumptionRecordsAsync(aggregatedRecords, cancellationToken);
    }

    private async Task CompleteProcessingLog(DataProcessingLog log)
    {
        log.Status = ProcessingStatus.Completed;
        log.CompletedAt = DateTime.UtcNow;
        await _processingLogRepository.UpdateLogAsync(log);
    }

    private async Task<ProcessingResultDto> HandleProcessingErrorAsync(
        DataProcessingLog log,
        string monthString,
        Exception ex,
        TimeSpan processingTime)
    {
        _logger.LogError(ex, "Unexpected error processing month {Month}", monthString);
        log.Status = ProcessingStatus.Failed;
        log.ErrorMessage = $"Unexpected error: {ex.Message}";
        log.CompletedAt = DateTime.UtcNow;
        await _processingLogRepository.UpdateLogAsync(log);

        return CreateFailureResult(monthString, ex.Message, processingTime);
    }

    private static ProcessingResultDto CreateSuccessResult(
        string month,
        int recordsProcessed,
        int recordsFiltered,
        int regionsAggregated,
        TimeSpan processingTime)
    {
        return new ProcessingResultDto
        {
            Success = true,
            Month = month,
            RecordsProcessed = recordsProcessed,
            RecordsFiltered = recordsFiltered,
            RegionsAggregated = regionsAggregated,
            ProcessingTime = processingTime
        };
    }

    private static ProcessingResultDto CreateFailureResult(string month, string errorMessage, TimeSpan processingTime)
    {
        return new ProcessingResultDto
        {
            Success = false,
            Month = month,
            ErrorMessage = errorMessage,
            ProcessingTime = processingTime
        };
    }
}
