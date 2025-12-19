namespace Electricity.Application.DTOs;

public class ProcessingResultDto
{
    public bool Success { get; set; }
    public string Month { get; set; } = string.Empty;
    public int RecordsProcessed { get; set; }
    public int RecordsFiltered { get; set; }
    public int RegionsAggregated { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ProcessingTime { get; set; }
}
