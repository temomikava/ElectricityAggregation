namespace Electricity.Application.DTOs;

public class RegionConsumptionDto
{
    public string Region { get; set; } = string.Empty;
    public DateTime Month { get; set; }
    public decimal TotalConsumption { get; set; }
    public int ApartmentCount { get; set; }
    public decimal AverageConsumption { get; set; }
}
