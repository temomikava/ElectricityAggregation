namespace Electricity.Infrastructure.Configuration;

public class DownloadOptions
{
    public const string SectionName = "Download";

    public int MaxRetries { get; set; } = 3;
    public double MaxDelaySeconds { get; set; } = 30;
}
