using Electricity.Application.DTOs;
using Electricity.Domain.ValueObjects;

namespace Electricity.Application.Interfaces;

public interface IElectricityDataService
{
    Task<ProcessingResultDto> ProcessMonthDataAsync(MonthYear monthYear, CancellationToken cancellationToken = default);
    Task<List<RegionConsumptionDto>> GetConsumptionByRegionAsync(DateTime? fromMonth = null, DateTime? toMonth = null);
    Task<List<DataProcessingLogDto>> GetProcessingHistoryAsync(int take = 10);
}
