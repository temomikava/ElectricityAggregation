using Electricity.Application.Interfaces;
using Electricity.Domain.ValueObjects;
using Electricity.Worker.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Electricity.Worker;

public class DataProcessingWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataProcessingWorker> _logger;
    private readonly WorkerConfiguration _config;

    public DataProcessingWorker(
        IServiceProvider serviceProvider,
        ILogger<DataProcessingWorker> logger,
        IOptions<WorkerConfiguration> config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data Processing Worker starting at {Time}", DateTimeOffset.Now);

        if (_config.ProcessOnStartup)
        {
            await ProcessDataAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delay = TimeSpan.FromMinutes(_config.ProcessingIntervalMinutes);
                _logger.LogInformation("Worker waiting {Minutes} minutes until next processing run", _config.ProcessingIntervalMinutes);
                await Task.Delay(delay, stoppingToken);

                await ProcessDataAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Worker service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker service main loop");
            }
        }

        _logger.LogInformation("Data Processing Worker stopped at {Time}", DateTimeOffset.Now);
    }

    private async Task ProcessDataAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting data processing cycle at {Time}", DateTimeOffset.Now);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dataService = scope.ServiceProvider.GetRequiredService<IElectricityDataService>();

            // Use configured latest available period instead of current date
            var latestMonth = new MonthYear(_config.LatestAvailableYear, _config.LatestAvailableMonth);
            var previousMonth = _config.LatestAvailableMonth == 1
                ? new MonthYear(_config.LatestAvailableYear - 1, 12)
                : new MonthYear(_config.LatestAvailableYear, _config.LatestAvailableMonth - 1);

            _logger.LogInformation("Processing latest available month: {Month}", latestMonth);
            var result1 = await dataService.ProcessMonthDataAsync(latestMonth, cancellationToken);
            LogProcessingResult(result1);

            _logger.LogInformation("Processing previous month: {Month}", previousMonth);
            var result2 = await dataService.ProcessMonthDataAsync(previousMonth, cancellationToken);
            LogProcessingResult(result2);

            _logger.LogInformation("Data processing cycle completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data processing cycle");
        }
    }

    private void LogProcessingResult(Application.DTOs.ProcessingResultDto result)
    {
        if (result.Success)
        {
            _logger.LogInformation(
                "Successfully processed {Month}: {RecordsProcessed} records processed, {RecordsFiltered} apartments, {Regions} regions aggregated in {Duration}",
                result.Month,
                result.RecordsProcessed,
                result.RecordsFiltered,
                result.RegionsAggregated,
                result.ProcessingTime);
        }
        else
        {
            _logger.LogError(
                "Failed to process {Month}: {Error}",
                result.Month,
                result.ErrorMessage);
        }
    }
}
