using Electricity.Application.DTOs;

namespace Electricity.Application.Interfaces;

public interface ICsvParser
{
    Task<List<RawElectricityDataDto>> ParseCsvAsync(Stream csvStream, CancellationToken cancellationToken = default);
}
