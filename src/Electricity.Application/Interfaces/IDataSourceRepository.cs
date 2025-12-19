namespace Electricity.Application.Interfaces;

public interface IDataSourceRepository
{
    Task<Stream> DownloadCsvFileAsync(string fileName, CancellationToken cancellationToken = default);
}
