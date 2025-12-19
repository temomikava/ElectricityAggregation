using Electricity.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Electricity.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ElectricityDbContext _dbContext;
    private readonly ILogger<HealthController> _logger;

    public HealthController(ElectricityDbContext dbContext, ILogger<HealthController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Check()
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync();

            if (canConnect)
            {
                _logger.LogInformation("Health check passed");
                return Ok(new
                {
                    healthy = true,
                    timestamp = DateTime.UtcNow,
                    database = "connected"
                });
            }
            else
            {
                _logger.LogWarning("Health check failed - database not connected");
                return StatusCode(503, new
                {
                    healthy = false,
                    timestamp = DateTime.UtcNow,
                    database = "disconnected"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check error");
            return StatusCode(503, new
            {
                healthy = false,
                timestamp = DateTime.UtcNow,
                error = ex.Message
            });
        }
    }
}
