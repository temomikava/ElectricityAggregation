using Electricity.Domain.Enums;

namespace Electricity.Domain.Entities;

public class DataProcessingLog
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Month { get; set; } = string.Empty;
    public ProcessingStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public int RecordsProcessed { get; set; }
    public int RecordsFiltered { get; set; }
}
