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
    /// <param name="fromMonth">Start month (optional, defaults to 2 months ago)</param>
    /// <param name="toMonth">End month (optional, defaults to current month)</param>
    /// <returns>List of consumption data aggregated by region</returns>
    /// <response code="200">Returns the consumption data</response>
    /// <response code="500">If an error occurs</response>
    [HttpGet("consumption")]
    [ProducesResponseType(typeof(List<RegionConsumptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<RegionConsumptionDto>>> GetConsumption(
        [FromQuery] DateTime? fromMonth,
        [FromQuery] DateTime? toMonth)
    {
        _logger.LogInformation("Getting consumption data from {From} to {To}", fromMonth, toMonth);
        var result = await _dataService.GetConsumptionByRegionAsync(fromMonth, toMonth);
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
