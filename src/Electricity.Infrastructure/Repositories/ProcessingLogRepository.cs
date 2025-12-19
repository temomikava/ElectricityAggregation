using Electricity.Application.Interfaces;
using Electricity.Domain.Entities;
using Electricity.Domain.Enums;
using Electricity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Electricity.Infrastructure.Repositories;

public class ProcessingLogRepository : IProcessingLogRepository
{
    private readonly ElectricityDbContext _context;

    public ProcessingLogRepository(ElectricityDbContext context)
    {
        _context = context;
    }

    public async Task<DataProcessingLog> CreateLogAsync(string month)
    {
        var log = new DataProcessingLog
        {
            Id = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            Month = month,
            Status = ProcessingStatus.Started,
            RecordsProcessed = 0,
            RecordsFiltered = 0
        };

        await _context.ProcessingLogs.AddAsync(log);
        await _context.SaveChangesAsync();

        return log;
    }

    public async Task UpdateLogAsync(DataProcessingLog log)
    {
        _context.ProcessingLogs.Update(log);
        await _context.SaveChangesAsync();
    }

    public async Task<List<DataProcessingLog>> GetRecentLogsAsync(int count)
    {
        return await _context.ProcessingLogs
            .OrderByDescending(l => l.StartedAt)
            .Take(count)
            .ToListAsync();
    }
}
