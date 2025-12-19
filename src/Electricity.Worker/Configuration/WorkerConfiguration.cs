namespace Electricity.Worker.Configuration;

public class WorkerConfiguration
{
    public int ProcessingIntervalMinutes { get; set; } = 60;
    public bool ProcessOnStartup { get; set; } = true;
    public int LatestAvailableYear { get; set; } = 2024;
    public int LatestAvailableMonth { get; set; } = 10;
}
