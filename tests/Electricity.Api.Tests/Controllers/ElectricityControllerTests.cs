using Electricity.Api.Controllers;
using Electricity.Application.DTOs;
using Electricity.Application.Interfaces;
using Electricity.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Electricity.Api.Tests.Controllers;

public class ElectricityControllerTests
{
    private readonly Mock<IElectricityDataService> _mockDataService;
    private readonly Mock<ILogger<ElectricityController>> _mockLogger;
    private readonly ElectricityController _controller;

    public ElectricityControllerTests()
    {
        _mockDataService = new Mock<IElectricityDataService>();
        _mockLogger = new Mock<ILogger<ElectricityController>>();
        _controller = new ElectricityController(_mockDataService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetConsumption_WithValidDates_ShouldReturnOkWithData()
    {
        // Arrange
        var fromMonth = new DateTime(2024, 9, 1);
        var toMonth = new DateTime(2024, 10, 31);
        var expectedData = new List<RegionConsumptionDto>
        {
            new RegionConsumptionDto
            {
                Region = "ESO",
                Month = new DateTime(2024, 10, 1),
                TotalConsumption = 100.5m,
                ApartmentCount = 10,
                AverageConsumption = 10.05m
            },
            new RegionConsumptionDto
            {
                Region = "Regionas2",
                Month = new DateTime(2024, 10, 1),
                TotalConsumption = 50.25m,
                ApartmentCount = 5,
                AverageConsumption = 10.05m
            }
        };

        _mockDataService
            .Setup(x => x.GetConsumptionByRegionAsync(fromMonth, toMonth))
            .ReturnsAsync(expectedData);

        // Act
        var result = await _controller.GetConsumption(fromMonth, toMonth);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedData);
        _mockDataService.Verify(x => x.GetConsumptionByRegionAsync(fromMonth, toMonth), Times.Once);
    }

    [Fact]
    public async Task GetConsumption_WithNullDates_ShouldReturnOkWithAllData()
    {
        // Arrange
        var expectedData = new List<RegionConsumptionDto>
        {
            new RegionConsumptionDto
            {
                Region = "ESO",
                Month = new DateTime(2024, 10, 1),
                TotalConsumption = 100.5m,
                ApartmentCount = 10,
                AverageConsumption = 10.05m
            }
        };

        _mockDataService
            .Setup(x => x.GetConsumptionByRegionAsync(null, null))
            .ReturnsAsync(expectedData);

        // Act
        var result = await _controller.GetConsumption(null, null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedData);
    }

    [Fact]
    public async Task GetConsumption_WhenServiceThrowsException_ShouldReturn500()
    {
        // Arrange
        _mockDataService
            .Setup(x => x.GetConsumptionByRegionAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetConsumption(null, null);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GetProcessingHistory_WithValidTake_ShouldReturnOkWithData()
    {
        // Arrange
        var expectedLogs = new List<DataProcessingLogDto>
        {
            new DataProcessingLogDto
            {
                Id = Guid.NewGuid(),
                StartedAt = DateTime.UtcNow.AddHours(-2),
                CompletedAt = DateTime.UtcNow.AddHours(-1),
                Month = "2024-10",
                Status = "Completed",
                RecordsProcessed = 100
            },
            new DataProcessingLogDto
            {
                Id = Guid.NewGuid(),
                StartedAt = DateTime.UtcNow.AddHours(-4),
                CompletedAt = DateTime.UtcNow.AddHours(-3),
                Month = "2024-09",
                Status = "Completed",
                RecordsProcessed = 95
            }
        };

        _mockDataService
            .Setup(x => x.GetProcessingHistoryAsync(10))
            .ReturnsAsync(expectedLogs);

        // Act
        var result = await _controller.GetProcessingHistory(10);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedLogs);
        _mockDataService.Verify(x => x.GetProcessingHistoryAsync(10), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(150)]
    public async Task GetProcessingHistory_WithInvalidTake_ShouldReturnBadRequest(int take)
    {
        // Act
        var result = await _controller.GetProcessingHistory(take);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        _mockDataService.Verify(x => x.GetProcessingHistoryAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetProcessingHistory_WhenServiceThrowsException_ShouldReturn500()
    {
        // Arrange
        _mockDataService
            .Setup(x => x.GetProcessingHistoryAsync(It.IsAny<int>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetProcessingHistory(10);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task TriggerProcessing_WithValidRequest_ShouldReturnOkWithResult()
    {
        // Arrange
        var request = new TriggerProcessingRequest { Year = 2024, Month = 10 };
        var expectedResult = new ProcessingResultDto
        {
            Success = true,
            Month = "2024-10",
            RecordsProcessed = 100,
            RecordsFiltered = 80,
            RegionsAggregated = 5,
            ProcessingTime = TimeSpan.FromSeconds(30)
        };

        _mockDataService
            .Setup(x => x.ProcessMonthDataAsync(It.Is<MonthYear>(m => m.Year == 2024 && m.Month == 10), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.TriggerProcessing(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedResult);
        _mockDataService.Verify(x => x.ProcessMonthDataAsync(
            It.Is<MonthYear>(m => m.Year == 2024 && m.Month == 10),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Theory]
    [InlineData(2019, 10)]  // Year too old
    [InlineData(2024, 0)]   // Month too low
    [InlineData(2024, 13)]  // Month too high
    [InlineData(2024, -1)]  // Negative month
    public async Task TriggerProcessing_WithInvalidRequest_ShouldReturnBadRequest(int year, int month)
    {
        // Arrange
        var request = new TriggerProcessingRequest { Year = year, Month = month };

        // Act
        var result = await _controller.TriggerProcessing(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        _mockDataService.Verify(x => x.ProcessMonthDataAsync(
            It.IsAny<MonthYear>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    [Fact]
    public async Task TriggerProcessing_WithNullRequest_ShouldReturnBadRequest()
    {
        // Act
        var result = await _controller.TriggerProcessing(null!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        _mockDataService.Verify(x => x.ProcessMonthDataAsync(
            It.IsAny<MonthYear>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    [Fact]
    public async Task TriggerProcessing_WhenServiceThrowsException_ShouldReturn500()
    {
        // Arrange
        var request = new TriggerProcessingRequest { Year = 2024, Month = 10 };
        _mockDataService
            .Setup(x => x.ProcessMonthDataAsync(It.IsAny<MonthYear>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Processing error"));

        // Act
        var result = await _controller.TriggerProcessing(request);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task TriggerProcessing_WithFailedProcessing_ShouldReturnOkWithFailureResult()
    {
        // Arrange
        var request = new TriggerProcessingRequest { Year = 2024, Month = 10 };
        var expectedResult = new ProcessingResultDto
        {
            Success = false,
            Month = "2024-10",
            ErrorMessage = "Failed to download CSV",
            ProcessingTime = TimeSpan.FromSeconds(5)
        };

        _mockDataService
            .Setup(x => x.ProcessMonthDataAsync(It.IsAny<MonthYear>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.TriggerProcessing(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(expectedResult);
    }
}
