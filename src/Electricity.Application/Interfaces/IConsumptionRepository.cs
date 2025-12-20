using Electricity.Domain.Entities;

namespace Electricity.Application.Interfaces;

public interface IConsumptionRepository
{
    Task AddConsumptionRecordsAsync(List<ElectricityConsumptionRecord> records, CancellationToken cancellationToken = default);
    Task<List<ElectricityConsumptionRecord>> GetByMonthRangeAsync(DateTime fromMonth, DateTime toMonth);
    Task<DateTime?> GetLatestMonthAsync();
    Task<bool> MonthExistsAsync(DateTime month);
    Task DeleteByMonthAsync(DateTime month);
}
