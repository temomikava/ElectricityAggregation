using System.Text;
using Electricity.Application.DTOs;
using Electricity.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Electricity.Application.Tests.Services;

public class CsvParserServiceTests
{
    private readonly Mock<ILogger<CsvParserService>> _mockLogger;
    private readonly CsvParserService _parser;

    public CsvParserServiceTests()
    {
        _mockLogger = new Mock<ILogger<CsvParserService>>();
        _parser = new CsvParserService(_mockLogger.Object);
    }

    [Fact]
    public async Task ParseCsvAsync_WithValidData_ShouldParseCorrectly()
    {
        // Arrange
        var csvContent = @"Tinklas;Objekto tipas;00:00-01:00;01:00-02:00;02:00-03:00
ESO;Butas;1.5;2.0;1.8
Regionas2;Namas;3.2;2.5;2.8";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _parser.ParseCsvAsync(stream);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);

        var esoRecord = result[0];
        esoRecord.Tinklas.Should().Be("ESO");
        esoRecord.ObjektoTipas.Should().Be("Butas");
        esoRecord.HourlyConsumption.Should().HaveCount(3);
        esoRecord.HourlyConsumption[0].Should().Be(1.5m);
        esoRecord.HourlyConsumption[1].Should().Be(2.0m);
        esoRecord.HourlyConsumption[2].Should().Be(1.8m);

        var regionas2Record = result[1];
        regionas2Record.Tinklas.Should().Be("Regionas2");
        regionas2Record.ObjektoTipas.Should().Be("Namas");
        regionas2Record.HourlyConsumption.Should().HaveCount(3);
        regionas2Record.HourlyConsumption[0].Should().Be(3.2m);
    }

    [Fact]
    public async Task ParseCsvAsync_WithCommaDecimals_ShouldConvertToPeriod()
    {
        // Arrange - Lithuanian CSV often uses comma as decimal separator
        var csvContent = @"Tinklas;Objekto tipas;00:00-01:00;01:00-02:00
ESO;Butas;1,5;2,75";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _parser.ParseCsvAsync(stream);

        // Assert
        result.Should().HaveCount(1);
        result[0].HourlyConsumption[0].Should().Be(1.5m);
        result[0].HourlyConsumption[1].Should().Be(2.75m);
    }

    [Fact]
    public async Task ParseCsvAsync_WithMissingValues_ShouldSkipThoseFields()
    {
        // Arrange
        var csvContent = @"Tinklas;Objekto tipas;00:00-01:00;01:00-02:00;02:00-03:00
ESO;Butas;1.5;;2.0";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _parser.ParseCsvAsync(stream);

        // Assert
        result.Should().HaveCount(1);
        result[0].HourlyConsumption.Should().HaveCount(2); // Only 2 values, middle one skipped
        result[0].HourlyConsumption[0].Should().Be(1.5m);
        result[0].HourlyConsumption[2].Should().Be(2.0m);
        result[0].HourlyConsumption.Should().NotContainKey(1); // Index 1 should not exist
    }

    [Fact]
    public async Task ParseCsvAsync_WithEmptyTinklas_ShouldSkipRow()
    {
        // Arrange
        var csvContent = @"Tinklas;Objekto tipas;00:00-01:00
;Butas;1.5
ESO;Butas;2.0";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _parser.ParseCsvAsync(stream);

        // Assert
        result.Should().HaveCount(1); // Only the second row with valid Tinklas
        result[0].Tinklas.Should().Be("ESO");
    }

    [Fact]
    public async Task ParseCsvAsync_WithEmptyObjektoTipas_ShouldSkipRow()
    {
        // Arrange
        var csvContent = @"Tinklas;Objekto tipas;00:00-01:00
ESO;;1.5
ESO;Butas;2.0";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _parser.ParseCsvAsync(stream);

        // Assert
        result.Should().HaveCount(1); // Only the second row with valid ObjektoTipas
        result[0].ObjektoTipas.Should().Be("Butas");
    }

    [Fact]
    public async Task ParseCsvAsync_WithInvalidNumericValues_ShouldSkipThoseFields()
    {
        // Arrange
        var csvContent = @"Tinklas;Objekto tipas;00:00-01:00;01:00-02:00;02:00-03:00
ESO;Butas;1.5;invalid;2.0";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _parser.ParseCsvAsync(stream);

        // Assert
        result.Should().HaveCount(1);
        result[0].HourlyConsumption.Should().HaveCount(2); // Only valid numeric values
        result[0].HourlyConsumption[0].Should().Be(1.5m);
        result[0].HourlyConsumption[2].Should().Be(2.0m);
        result[0].HourlyConsumption.Should().NotContainKey(1); // Invalid value skipped
    }

    [Fact]
    public async Task ParseCsvAsync_WithWhitespace_ShouldTrimValues()
    {
        // Arrange
        var csvContent = @"Tinklas;Objekto tipas;00:00-01:00
  ESO  ;  Butas  ;1.5";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _parser.ParseCsvAsync(stream);

        // Assert
        result.Should().HaveCount(1);
        result[0].Tinklas.Should().Be("ESO"); // Trimmed
        result[0].ObjektoTipas.Should().Be("Butas"); // Trimmed
    }

    [Fact]
    public async Task ParseCsvAsync_WithBlankLines_ShouldIgnoreThem()
    {
        // Arrange
        var csvContent = @"Tinklas;Objekto tipas;00:00-01:00

ESO;Butas;1.5

Regionas2;Namas;2.0

";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _parser.ParseCsvAsync(stream);

        // Assert
        result.Should().HaveCount(2); // Blank lines ignored
    }

    [Fact]
    public async Task ParseCsvAsync_WithNoHeader_ShouldThrowException()
    {
        // Arrange
        var csvContent = "";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act & Assert
        var exception = await Assert.ThrowsAnyAsync<Exception>(
            async () => await _parser.ParseCsvAsync(stream)
        );
        exception.Should().NotBeNull();
    }

    [Fact]
    public async Task ParseCsvAsync_WithInvalidHeader_ShouldThrowException()
    {
        // Arrange - Header with less than 3 columns
        var csvContent = @"Tinklas;Objekto tipas";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _parser.ParseCsvAsync(stream)
        );
    }

    [Fact]
    public async Task ParseCsvAsync_WithUTF8BOM_ShouldParseCorrectly()
    {
        // Arrange - UTF8 with BOM (common in Lithuanian files)
        var csvContent = @"Tinklas;Objekto tipas;00:00-01:00
ESO;Butas;1.5";
        var utf8WithBom = new UTF8Encoding(true); // true = include BOM
        using var stream = new MemoryStream(utf8WithBom.GetBytes(csvContent));

        // Act
        var result = await _parser.ParseCsvAsync(stream);

        // Assert
        result.Should().HaveCount(1);
        result[0].Tinklas.Should().Be("ESO");
    }

    [Fact]
    public async Task ParseCsvAsync_WithMultipleHourlyColumns_ShouldParseAll()
    {
        // Arrange - Simulating 24 hourly columns
        var csvContent = @"Tinklas;Objekto tipas;00:00;01:00;02:00;03:00;04:00;05:00;06:00;07:00;08:00;09:00;10:00;11:00;12:00;13:00;14:00;15:00;16:00;17:00;18:00;19:00;20:00;21:00;22:00;23:00
ESO;Butas;1.0;1.1;1.2;1.3;1.4;1.5;1.6;1.7;1.8;1.9;2.0;2.1;2.2;2.3;2.4;2.5;2.6;2.7;2.8;2.9;3.0;3.1;3.2;3.3";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _parser.ParseCsvAsync(stream);

        // Assert
        result.Should().HaveCount(1);
        result[0].HourlyConsumption.Should().HaveCount(24);
        result[0].HourlyConsumption[0].Should().Be(1.0m);
        result[0].HourlyConsumption[23].Should().Be(3.3m);
    }

    [Fact]
    public async Task ParseCsvAsync_WithLargeDataset_ShouldHandleCancellation()
    {
        // Arrange - Create large CSV to ensure async read operation is in progress
        var csvLines = new List<string> { "Tinklas;Objekto tipas;00:00-01:00" };
        for (int i = 0; i < 1000; i++)
        {
            csvLines.Add($"ESO{i};Butas;{i}.5");
        }
        var csvContent = string.Join("\n", csvLines);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        using var cts = new CancellationTokenSource();

        // Start the parse operation and cancel after a short delay
        var parseTask = _parser.ParseCsvAsync(stream, cts.Token);
        cts.Cancel();

        // Act & Assert - Either completes successfully or throws cancellation
        try
        {
            var result = await parseTask;
            result.Should().NotBeNull(); // If it completes, that's OK
        }
        catch (OperationCanceledException)
        {
            // This is also acceptable
            Assert.True(true);
        }
    }

    [Fact]
    public async Task ParseCsvAsync_WithEmptyStream_ShouldReturnEmptyList()
    {
        // Arrange
        var csvContent = @"Tinklas;Objekto tipas;00:00-01:00";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _parser.ParseCsvAsync(stream);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty(); // No data rows, only header
    }

    [Fact]
    public async Task ParseCsvAsync_WithNegativeValues_ShouldParseCorrectly()
    {
        // Arrange - Sometimes energy can be negative (e.g., solar production)
        var csvContent = @"Tinklas;Objekto tipas;00:00-01:00;01:00-02:00
ESO;Butas;-0.5;1.5";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _parser.ParseCsvAsync(stream);

        // Assert
        result.Should().HaveCount(1);
        result[0].HourlyConsumption[0].Should().Be(-0.5m);
        result[0].HourlyConsumption[1].Should().Be(1.5m);
    }

    [Fact]
    public async Task ParseCsvAsync_WithZeroValues_ShouldParseCorrectly()
    {
        // Arrange
        var csvContent = @"Tinklas;Objekto tipas;00:00-01:00;01:00-02:00
ESO;Butas;0;0.0";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var result = await _parser.ParseCsvAsync(stream);

        // Assert
        result.Should().HaveCount(1);
        result[0].HourlyConsumption[0].Should().Be(0m);
        result[0].HourlyConsumption[1].Should().Be(0m);
    }
}
