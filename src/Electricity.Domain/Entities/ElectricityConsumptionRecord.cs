namespace Electricity.Domain.Entities;

public class ElectricityConsumptionRecord
{
    public Guid Id { get; set; }
    public string Region { get; set; } = string.Empty;
    public string BuildingType { get; set; } = string.Empty;
    public DateTime Month { get; set; }
    public decimal TotalConsumption { get; set; }
    public int RecordCount { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string SourceFile { get; set; } = string.Empty;
}
