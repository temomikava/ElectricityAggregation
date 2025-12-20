using System.ComponentModel.DataAnnotations;
using Electricity.Application.DTOs;
using Electricity.Application.Interfaces;
using Electricity.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Electricity.Api.Controllers;

/// <summary>
/// Electricity consumption data endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ElectricityController : ControllerBase
{
    private readonly IElectricityDataService _dataService;
    private readonly ILogger<ElectricityController> _logger;

    public ElectricityController(IElectricityDataService dataService, ILogger<ElectricityController> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    /// <summary>
    /// Get aggregated electricity consumption by region
    /// </summary>
    /// <param name="fromMonth">
    /// Start month - accepts flexible formats: "2024-1", "2024-01", or "2024-01-01".
    /// Any day value is normalized to the 1st of the month.
    /// Optional - defaults to 2 months before latest available data.
    /// </param>
    /// <param name="toMonth">
    /// End month - accepts flexible formats: "2024-12", "2024-10", or "2024-10-01".
    /// Any day value is normalized to the 1st of the month.
    /// Optional - defaults to latest available data.
    /// </param>
    /// <returns>List of consumption data aggregated by region and month</returns>
    /// <response code="200">Returns the consumption data grouped by region (Tinklas) and month</response>
    /// <response code="500">If an error occurs while retrieving data</response>
    /// <remarks>
    /// Example requests:
    ///
    ///     GET /api/electricity/consumption?fromMonth=2024-1&amp;toMonth=2024-10
    ///     GET /api/electricity/consumption?fromMonth=2024-09&amp;toMonth=2024-12
    ///     GET /api/electricity/consumption?fromMonth=2024-01-01&amp;toMonth=2024-12-01
    ///     GET /api/electricity/consumption
    ///
    /// Returns apartment electricity consumption aggregated by region (Tinklas field).
    /// **Default behavior:** If no parameters provided, returns last 2 months of available data.
    /// </remarks>
    [HttpGet("consumption")]
    [ProducesResponseType(typeof(List<RegionConsumptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<RegionConsumptionDto>>> GetConsumption(
        [FromQuery] DateTime? fromMonth,
        [FromQuery] DateTime? toMonth)
    {
        var fromUtc = fromMonth.HasValue
            ? new DateTime(fromMonth.Value.Year, fromMonth.Value.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            : (DateTime?)null;
        var toUtc = toMonth.HasValue
            ? new DateTime(toMonth.Value.Year, toMonth.Value.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            : (DateTime?)null;

        _logger.LogInformation("Retrieving consumption data from {From:yyyy-MM} to {To:yyyy-MM}",
            fromUtc, toUtc);

        var result = await _dataService.GetConsumptionByRegionAsync(fromUtc, toUtc);
        return Ok(result);
    }

    /// <summary>
    /// Get processing history logs
    /// </summary>
    /// <param name="take">Number of recent logs to retrieve (1-100)</param>
    /// <returns>List of recent processing logs</returns>
    /// <response code="200">Returns the processing history</response>
    /// <response code="400">If take parameter is invalid</response>
    /// <response code="500">If an error occurs</response>
    [HttpGet("history")]
    [ProducesResponseType(typeof(List<DataProcessingLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<DataProcessingLogDto>>> GetProcessingHistory(
        [FromQuery][Range(1, 100)] int take = 10)
    {
        if (take < 1 || take > 100)
        {
            return BadRequest(new ValidationProblemDetails
            {
                Title = "Validation failed",
                Detail = "The take parameter must be between 1 and 100.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("Getting processing history (take: {Take})", take);
        var result = await _dataService.GetProcessingHistoryAsync(take);
        return Ok(result);
    }

    /// <summary>
    /// Manually trigger data processing for a specific month
    /// </summary>
    /// <param name="year">Year (2020-2100)</param>
    /// <param name="month">Month (1-12)</param>
    /// <returns>Processing result with statistics</returns>
    /// <response code="200">Returns the processing result</response>
    /// <response code="400">If year or month is invalid</response>
    /// <response code="500">If an error occurs during processing</response>
    [HttpPost("process/{year}/{month}")]
    [ProducesResponseType(typeof(ProcessingResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProcessingResultDto>> TriggerProcessing(
        [FromRoute][Range(2020, 2100)] int year,
        [FromRoute][Range(1, 12)] int month)
    {
        _logger.LogInformation("Manually triggering processing for {Year}-{Month:D2}", year, month);

        var monthYear = new MonthYear(year, month);
        var result = await _dataService.ProcessMonthDataAsync(monthYear);

        if (!result.Success)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, result);
        }

        return Ok(result);
    }
}
