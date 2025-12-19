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
            log.Status = ProcessingStatus.Downloading;
            await _processingLogRepository.UpdateLogAsync(log);

            _logger.LogInformation("Downloading CSV file {FileName}", fileName);
            Stream csvStream;
            try
            {
                csvStream = await _dataSourceRepository.DownloadCsvFileAsync(fileName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download CSV file {FileName}", fileName);
                log.Status = ProcessingStatus.Failed;
                log.ErrorMessage = $"Failed to download CSV: {ex.Message}";
                log.CompletedAt = DateTime.UtcNow;
                await _processingLogRepository.UpdateLogAsync(log);

                return new ProcessingResultDto
                {
                    Success = false,
                    Month = monthString,
                    ErrorMessage = ex.Message,
                    ProcessingTime = stopwatch.Elapsed
                };
            }

            log.Status = ProcessingStatus.Parsing;
            await _processingLogRepository.UpdateLogAsync(log);

            _logger.LogInformation("Parsing CSV file {FileName}", fileName);
            List<RawElectricityDataDto> rawData;
            try
            {
                await using (csvStream)
                {
                    rawData = await _csvParser.ParseCsvAsync(csvStream, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse CSV file {FileName}", fileName);
                log.Status = ProcessingStatus.Failed;
                log.ErrorMessage = $"Failed to parse CSV: {ex.Message}";
                log.CompletedAt = DateTime.UtcNow;
                await _processingLogRepository.UpdateLogAsync(log);

                return new ProcessingResultDto
                {
                    Success = false,
                    Month = monthString,
                    ErrorMessage = ex.Message,
                    ProcessingTime = stopwatch.Elapsed
                };
            }

            var totalRecords = rawData.Count;
            _logger.LogInformation("Parsed {Count} records from {FileName}", totalRecords, fileName);

            var apartmentData = FilterApartments(rawData);
            var filteredCount = apartmentData.Count;
            _logger.LogInformation("Filtered to {FilteredCount} apartment records from {TotalCount} total records",
                filteredCount, totalRecords);

            log.RecordsProcessed = totalRecords;
            log.RecordsFiltered = filteredCount;

            log.Status = ProcessingStatus.Aggregating;
            await _processingLogRepository.UpdateLogAsync(log);

            var aggregatedRecords = AggregateByRegion(apartmentData, monthYear);
            _logger.LogInformation("Aggregated data into {RegionCount} regions", aggregatedRecords.Count);

            var monthDateTime = monthYear.ToDateTime();
            if (await _consumptionRepository.MonthExistsAsync(monthDateTime))
            {
                _logger.LogInformation("Month {Month} already exists, deleting old data", monthString);
                await _consumptionRepository.DeleteByMonthAsync(monthDateTime);
            }

            log.Status = ProcessingStatus.Saving;
            await _processingLogRepository.UpdateLogAsync(log);

            _logger.LogInformation("Saving {Count} consumption records to database", aggregatedRecords.Count);
            await _consumptionRepository.AddConsumptionRecordsAsync(aggregatedRecords, cancellationToken);

            log.Status = ProcessingStatus.Completed;
            log.CompletedAt = DateTime.UtcNow;
            await _processingLogRepository.UpdateLogAsync(log);

            stopwatch.Stop();
            _logger.LogInformation("Completed processing for month {Month} in {Duration}ms",
                monthString, stopwatch.ElapsedMilliseconds);

            return new ProcessingResultDto
            {
                Success = true,
                Month = monthString,
                RecordsProcessed = totalRecords,
                RecordsFiltered = filteredCount,
                RegionsAggregated = aggregatedRecords.Count,
                ProcessingTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing month {Month}", monthString);
            log.Status = ProcessingStatus.Failed;
            log.ErrorMessage = $"Unexpected error: {ex.Message}";
            log.CompletedAt = DateTime.UtcNow;
            await _processingLogRepository.UpdateLogAsync(log);

            return new ProcessingResultDto
            {
                Success = false,
                Month = monthString,
                ErrorMessage = ex.Message,
                ProcessingTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<List<RegionConsumptionDto>> GetConsumptionByRegionAsync(DateTime? fromMonth = null, DateTime? toMonth = null)
    {
        var from = fromMonth ?? DateTime.UtcNow.AddMonths(-2);
        var to = toMonth ?? DateTime.UtcNow;

        _logger.LogInformation("Retrieving consumption data from {From} to {To}", from, to);

        var records = await _consumptionRepository.GetByMonthRangeAsync(from, to);

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

        _logger.LogInformation("Retrieved {Count} consumption records", result.Count);
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

    private List<RawElectricityDataDto> FilterApartments(List<RawElectricityDataDto> rawData)
    {
        return rawData
            .Where(r => r.ObjektoTipas.Equals("Butas", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private List<ElectricityConsumptionRecord> AggregateByRegion(List<RawElectricityDataDto> apartmentData, MonthYear monthYear)
    {
        var grouped = apartmentData
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

        return grouped;
    }
}
