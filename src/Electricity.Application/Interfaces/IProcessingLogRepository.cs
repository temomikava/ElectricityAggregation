using Electricity.Domain.Entities;

namespace Electricity.Application.Interfaces;

public interface IProcessingLogRepository
{
    Task<DataProcessingLog> CreateLogAsync(string month);
    Task UpdateLogAsync(DataProcessingLog log);
    Task<List<DataProcessingLog>> GetRecentLogsAsync(int count);
}
