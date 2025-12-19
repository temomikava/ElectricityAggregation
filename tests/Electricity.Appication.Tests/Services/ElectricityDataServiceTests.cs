using Electricity.Application.DTOs;
using Electricity.Application.Interfaces;
using Electricity.Application.Services;
using Electricity.Domain.Entities;
using Electricity.Domain.Enums;
using Electricity.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Electricity.Application.Tests.Services;

public class ElectricityDataServiceTests
{
    private readonly Mock<IDataSourceRepository> _mockDataSource;
    private readonly Mock<ICsvParser> _mockCsvParser;
    private readonly Mock<IConsumptionRepository> _mockConsumptionRepo;
    private readonly Mock<IProcessingLogRepository> _mockProcessingLogRepo;
    private readonly Mock<ILogger<ElectricityDataService>> _mockLogger;
    private readonly ElectricityDataService _service;

    public ElectricityDataServiceTests()
    {
        _mockDataSource = new Mock<IDataSourceRepository>();
        _mockCsvParser = new Mock<ICsvParser>();
        _mockConsumptionRepo = new Mock<IConsumptionRepository>();
        _mockProcessingLogRepo = new Mock<IProcessingLogRepository>();
        _mockLogger = new Mock<ILogger<ElectricityDataService>>();

        _service = new ElectricityDataService(
            _mockDataSource.Object,
            _mockCsvParser.Object,
            _mockConsumptionRepo.Object,
            _mockProcessingLogRepo.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task ProcessMonthDataAsync_WithValidData_ShouldAggregateCorrectly()
    {
        // Arrange
        var monthYear = new MonthYear(2024, 10);
        var processingLog = new DataProcessingLog
        {
            Id = Guid.NewGuid(),
            Month = "2024-10",
            Status = ProcessingStatus.Started
        };

        var rawData = new List<RawElectricityDataDto>
        {
            new RawElectricityDataDto
            {
                Tinklas = "ESO",
                ObjektoTipas = "Butas",
                HourlyConsumption = new Dictionary<int, decimal> { { 0, 1.5m }, { 1, 2.0m } }
            },
            new RawElectricityDataDto
            {
                Tinklas = "ESO",
                ObjektoTipas = "Butas",
                HourlyConsumption = new Dictionary<int, decimal> { { 0, 1.0m }, { 1, 1.5m } }
            },
            new RawElectricityDataDto
            {
                Tinklas = "Regionas2",
                ObjektoTipas = "Butas",
                HourlyConsumption = new Dictionary<int, decimal> { { 0, 0.5m }, { 1, 1.0m } }
            }
        };

        _mockProcessingLogRepo
            .Setup(x => x.CreateLogAsync(It.IsAny<string>()))
            .ReturnsAsync(processingLog);

        _mockDataSource
            .Setup(x => x.DownloadCsvFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        _mockCsvParser
            .Setup(x => x.ParseCsvAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rawData);

        _mockConsumptionRepo
            .Setup(x => x.MonthExistsAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.ProcessMonthDataAsync(monthYear);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RecordsProcessed.Should().Be(3);
        result.RecordsFiltered.Should().Be(3);
        result.RegionsAggregated.Should().Be(2);

        _mockConsumptionRepo.Verify(x => x.AddConsumptionRecordsAsync(
            It.Is<List<ElectricityConsumptionRecord>>(records =>
                records.Count == 2 &&
                records.Any(r => r.Region == "ESO" && r.TotalConsumption == 6.0m && r.RecordCount == 2) &&
                records.Any(r => r.Region == "Regionas2" && r.TotalConsumption == 1.5m && r.RecordCount == 1)
            ),
            It.IsAny<CancellationToken>()
        ), Times.Once);

        _mockProcessingLogRepo.Verify(x => x.UpdateLogAsync(
            It.Is<DataProcessingLog>(log => log.Status == ProcessingStatus.Completed)
        ), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessMonthDataAsync_WithNoApartments_ShouldReturnZeroRecords()
    {
        // Arrange
        var monthYear = new MonthYear(2024, 10);
        var processingLog = new DataProcessingLog
        {
            Id = Guid.NewGuid(),
            Month = "2024-10",
            Status = ProcessingStatus.Started
        };

        var rawData = new List<RawElectricityDataDto>
        {
            new RawElectricityDataDto
            {
                Tinklas = "ESO",
                ObjektoTipas = "Namas",  // House, not apartment
                HourlyConsumption = new Dictionary<int, decimal> { { 0, 1.5m } }
            }
        };

        _mockProcessingLogRepo
            .Setup(x => x.CreateLogAsync(It.IsAny<string>()))
            .ReturnsAsync(processingLog);

        _mockDataSource
            .Setup(x => x.DownloadCsvFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        _mockCsvParser
            .Setup(x => x.ParseCsvAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rawData);

        _mockConsumptionRepo
            .Setup(x => x.MonthExistsAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.ProcessMonthDataAsync(monthYear);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RecordsProcessed.Should().Be(1);
        result.RecordsFiltered.Should().Be(0);
        result.RegionsAggregated.Should().Be(0);

        _mockConsumptionRepo.Verify(x => x.AddConsumptionRecordsAsync(
            It.Is<List<ElectricityConsumptionRecord>>(records => records.Count == 0),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task ProcessMonthDataAsync_WithDownloadFailure_ShouldLogError()
    {
        // Arrange
        var monthYear = new MonthYear(2024, 10);
        var processingLog = new DataProcessingLog
        {
            Id = Guid.NewGuid(),
            Month = "2024-10",
            Status = ProcessingStatus.Started
        };

        _mockProcessingLogRepo
            .Setup(x => x.CreateLogAsync(It.IsAny<string>()))
            .ReturnsAsync(processingLog);

        _mockDataSource
            .Setup(x => x.DownloadCsvFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Download failed"));

        // Act
        var result = await _service.ProcessMonthDataAsync(monthYear);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Download failed");

        _mockProcessingLogRepo.Verify(x => x.UpdateLogAsync(
            It.Is<DataProcessingLog>(log =>
                log.Status == ProcessingStatus.Failed &&
                log.ErrorMessage != null
            )
        ), Times.AtLeastOnce);

        _mockCsvParser.Verify(x => x.ParseCsvAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMonthDataAsync_WithParsingError_ShouldReturnFailure()
    {
        // Arrange
        var monthYear = new MonthYear(2024, 10);
        var processingLog = new DataProcessingLog
        {
            Id = Guid.NewGuid(),
            Month = "2024-10",
            Status = ProcessingStatus.Started
        };

        _mockProcessingLogRepo
            .Setup(x => x.CreateLogAsync(It.IsAny<string>()))
            .ReturnsAsync(processingLog);

        _mockDataSource
            .Setup(x => x.DownloadCsvFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        _mockCsvParser
            .Setup(x => x.ParseCsvAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("CSV parsing error"));

        // Act
        var result = await _service.ProcessMonthDataAsync(monthYear);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("CSV parsing error");

        _mockProcessingLogRepo.Verify(x => x.UpdateLogAsync(
            It.Is<DataProcessingLog>(log => log.Status == ProcessingStatus.Failed)
        ), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("Butas", true)]
    [InlineData("butas", true)]  // Case insensitive
    [InlineData("BUTAS", true)]
    [InlineData("Namas", false)]
    [InlineData("Įmonė", false)]
    public async Task ProcessMonthDataAsync_ShouldFilterByBuildingType(string buildingType, bool shouldInclude)
    {
        // Arrange
        var monthYear = new MonthYear(2024, 10);
        var processingLog = new DataProcessingLog
        {
            Id = Guid.NewGuid(),
            Month = "2024-10",
            Status = ProcessingStatus.Started
        };

        var rawData = new List<RawElectricityDataDto>
        {
            new RawElectricityDataDto
            {
                Tinklas = "ESO",
                ObjektoTipas = buildingType,
                HourlyConsumption = new Dictionary<int, decimal> { { 0, 1.0m } }
            }
        };

        _mockProcessingLogRepo
            .Setup(x => x.CreateLogAsync(It.IsAny<string>()))
            .ReturnsAsync(processingLog);

        _mockDataSource
            .Setup(x => x.DownloadCsvFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        _mockCsvParser
            .Setup(x => x.ParseCsvAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rawData);

        _mockConsumptionRepo
            .Setup(x => x.MonthExistsAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.ProcessMonthDataAsync(monthYear);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RecordsFiltered.Should().Be(shouldInclude ? 1 : 0);
        result.RegionsAggregated.Should().Be(shouldInclude ? 1 : 0);
    }

    [Fact]
    public async Task GetConsumptionByRegionAsync_ShouldReturnAggregatedData()
    {
        // Arrange
        var fromMonth = new DateTime(2024, 9, 1);
        var toMonth = new DateTime(2024, 10, 31);

        var records = new List<ElectricityConsumptionRecord>
        {
            new ElectricityConsumptionRecord
            {
                Region = "ESO",
                Month = new DateTime(2024, 10, 1),
                TotalConsumption = 100.5m,
                RecordCount = 10
            },
            new ElectricityConsumptionRecord
            {
                Region = "Regionas2",
                Month = new DateTime(2024, 10, 1),
                TotalConsumption = 50.25m,
                RecordCount = 5
            }
        };

        _mockConsumptionRepo
            .Setup(x => x.GetByMonthRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(records);

        // Act
        var result = await _service.GetConsumptionByRegionAsync(fromMonth, toMonth);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);

        var esoRecord = result.First(r => r.Region == "ESO");
        esoRecord.TotalConsumption.Should().Be(100.5m);
        esoRecord.ApartmentCount.Should().Be(10);
        esoRecord.AverageConsumption.Should().Be(10.05m);

        var regionas2Record = result.First(r => r.Region == "Regionas2");
        regionas2Record.TotalConsumption.Should().Be(50.25m);
        regionas2Record.ApartmentCount.Should().Be(5);
        regionas2Record.AverageConsumption.Should().Be(10.05m);
    }

    [Fact]
    public async Task GetConsumptionByRegionAsync_WithNoData_ShouldReturnEmptyList()
    {
        // Arrange
        _mockConsumptionRepo
            .Setup(x => x.GetByMonthRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<ElectricityConsumptionRecord>());

        // Act
        var result = await _service.GetConsumptionByRegionAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProcessingHistoryAsync_ShouldReturnRecentLogs()
    {
        // Arrange
        var logs = new List<DataProcessingLog>
        {
            new DataProcessingLog
            {
                Id = Guid.NewGuid(),
                StartedAt = DateTime.UtcNow.AddHours(-2),
                CompletedAt = DateTime.UtcNow.AddHours(-1),
                Month = "2024-10",
                Status = ProcessingStatus.Completed,
                RecordsProcessed = 100
            },
            new DataProcessingLog
            {
                Id = Guid.NewGuid(),
                StartedAt = DateTime.UtcNow.AddHours(-4),
                CompletedAt = DateTime.UtcNow.AddHours(-3),
                Month = "2024-09",
                Status = ProcessingStatus.Completed,
                RecordsProcessed = 95
            }
        };

        _mockProcessingLogRepo
            .Setup(x => x.GetRecentLogsAsync(It.IsAny<int>()))
            .ReturnsAsync(logs);

        // Act
        var result = await _service.GetProcessingHistoryAsync(10);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.First().Month.Should().Be("2024-10");
        result.First().Status.Should().Be("Completed");
        result.First().RecordsProcessed.Should().Be(100);
    }

    [Fact]
    public async Task ProcessMonthDataAsync_WhenMonthExists_ShouldDeleteOldDataFirst()
    {
        // Arrange
        var monthYear = new MonthYear(2024, 10);
        var processingLog = new DataProcessingLog
        {
            Id = Guid.NewGuid(),
            Month = "2024-10",
            Status = ProcessingStatus.Started
        };

        var rawData = new List<RawElectricityDataDto>
        {
            new RawElectricityDataDto
            {
                Tinklas = "ESO",
                ObjektoTipas = "Butas",
                HourlyConsumption = new Dictionary<int, decimal> { { 0, 1.0m } }
            }
        };

        _mockProcessingLogRepo
            .Setup(x => x.CreateLogAsync(It.IsAny<string>()))
            .ReturnsAsync(processingLog);

        _mockDataSource
            .Setup(x => x.DownloadCsvFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        _mockCsvParser
            .Setup(x => x.ParseCsvAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rawData);

        _mockConsumptionRepo
            .Setup(x => x.MonthExistsAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ProcessMonthDataAsync(monthYear);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        _mockConsumptionRepo.Verify(x => x.DeleteByMonthAsync(It.IsAny<DateTime>()), Times.Once);
        _mockConsumptionRepo.Verify(x => x.AddConsumptionRecordsAsync(
            It.IsAny<List<ElectricityConsumptionRecord>>(),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }
}
