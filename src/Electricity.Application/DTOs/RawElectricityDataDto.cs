namespace Electricity.Application.DTOs;

public class RawElectricityDataDto
{
    public string Tinklas { get; set; } = string.Empty;
    public string ObjektoTipas { get; set; } = string.Empty;
    public Dictionary<int, decimal> HourlyConsumption { get; set; } = new();
}
