using Electricity.Application.Interfaces;
using Electricity.Domain.Entities;
using Electricity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Electricity.Infrastructure.Repositories;

public class ConsumptionRepository : IConsumptionRepository
{
    private readonly ElectricityDbContext _context;

    public ConsumptionRepository(ElectricityDbContext context)
    {
        _context = context;
    }

    public async Task AddConsumptionRecordsAsync(List<ElectricityConsumptionRecord> records, CancellationToken cancellationToken = default)
    {
        await _context.ConsumptionRecords.AddRangeAsync(records, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ElectricityConsumptionRecord>> GetByMonthRangeAsync(DateTime fromMonth, DateTime toMonth)
    {
        return await _context.ConsumptionRecords
            .Where(r => r.Month >= fromMonth && r.Month <= toMonth)
            .OrderBy(r => r.Month)
            .ThenBy(r => r.Region)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<bool> MonthExistsAsync(DateTime month)
    {
        return await _context.ConsumptionRecords
            .AsNoTracking()
            .AnyAsync(r => r.Month == month);
    }

    public async Task DeleteByMonthAsync(DateTime month)
    {
        await _context.ConsumptionRecords
            .Where(r => r.Month == month)
            .ExecuteDeleteAsync();
    }
}
