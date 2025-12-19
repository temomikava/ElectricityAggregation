using Electricity.Application.DTOs;
using Electricity.Application.Interfaces;
using Electricity.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Electricity.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ElectricityController : ControllerBase
{
    private readonly IElectricityDataService _dataService;
    private readonly ILogger<ElectricityController> _logger;

    public ElectricityController(IElectricityDataService dataService, ILogger<ElectricityController> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    [HttpGet("consumption")]
    [ProducesResponseType(typeof(List<RegionConsumptionDto>), 200)]
    public async Task<IActionResult> GetConsumption(
        [FromQuery] DateTime? fromMonth,
        [FromQuery] DateTime? toMonth)
    {
        try
        {
            _logger.LogInformation("Getting consumption data from {From} to {To}", fromMonth, toMonth);
            var result = await _dataService.GetConsumptionByRegionAsync(fromMonth, toMonth);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving consumption data");
            return StatusCode(500, new { error = "An error occurred while retrieving consumption data" });
        }
    }

    [HttpGet("processing-history")]
    [ProducesResponseType(typeof(List<DataProcessingLogDto>), 200)]
    public async Task<IActionResult> GetProcessingHistory([FromQuery] int take = 10)
    {
        try
        {
            if (take < 1 || take > 100)
            {
                return BadRequest(new { error = "Take parameter must be between 1 and 100" });
            }

            _logger.LogInformation("Getting processing history (take: {Take})", take);
            var result = await _dataService.GetProcessingHistoryAsync(take);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving processing history");
            return StatusCode(500, new { error = "An error occurred while retrieving processing history" });
        }
    }

    [HttpPost("trigger-processing")]
    [ProducesResponseType(typeof(ProcessingResultDto), 200)]
    public async Task<IActionResult> TriggerProcessing([FromBody] TriggerProcessingRequest request)
    {
        try
        {
            if (request == null || request.Year < 2020 || request.Month < 1 || request.Month > 12)
            {
                return BadRequest(new { error = "Invalid year or month" });
            }

            _logger.LogInformation("Manually triggering processing for {Year}-{Month}", request.Year, request.Month);

            var monthYear = new MonthYear(request.Year, request.Month);
            var result = await _dataService.ProcessMonthDataAsync(monthYear);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering processing");
            return StatusCode(500, new { error = "An error occurred while triggering processing" });
        }
    }
}

public class TriggerProcessingRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
}
