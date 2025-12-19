namespace Electricity.Application.DTOs;

public class DataProcessingLogDto
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Month { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int RecordsProcessed { get; set; }
}
