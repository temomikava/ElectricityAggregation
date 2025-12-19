using System.Text.RegularExpressions;
using Electricity.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Electricity.Infrastructure.ExternalServices;

public class DataGovLtDataSourceRepository : IDataSourceRepository
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DataGovLtDataSourceRepository> _logger;
    private const string DatasetPageUrl = "https://data.gov.lt/datasets/1975/";
    private const string BaseUrl = "https://data.gov.lt";
    private const int MaxRetries = 3;

    public DataGovLtDataSourceRepository(HttpClient httpClient, ILogger<DataGovLtDataSourceRepository> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<Stream> DownloadCsvFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        // First, get the actual download URL from the dataset page
        var downloadUrl = await GetDownloadUrlAsync(fileName, cancellationToken);
        if (string.IsNullOrEmpty(downloadUrl))
        {
            throw new InvalidOperationException($"Could not find download URL for file {fileName}");
        }

        var retryCount = 0;
        var url = downloadUrl;

        while (retryCount < MaxRetries)
        {
            try
            {
                _logger.LogInformation("Attempting to download CSV file from {Url} (Attempt {Attempt}/{MaxRetries})",
                    url, retryCount + 1, MaxRetries);

                var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully downloaded CSV file {FileName}", fileName);
                    var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream, cancellationToken);
                    memoryStream.Position = 0;
                    return memoryStream;
                }

                _logger.LogWarning("Failed to download CSV file {FileName}. Status code: {StatusCode}",
                    fileName, response.StatusCode);

                retryCount++;

                if (retryCount < MaxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                    _logger.LogInformation("Retrying in {Delay} seconds...", delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout while downloading CSV file {FileName}", fileName);
                throw new TimeoutException($"Timeout while downloading {fileName}", ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while downloading CSV file {FileName}", fileName);
                retryCount++;

                if (retryCount >= MaxRetries)
                {
                    throw;
                }

                var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException($"Failed to download CSV file {fileName} after {MaxRetries} attempts");
    }

    private async Task<string?> GetDownloadUrlAsync(string fileName, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching dataset page to find download URL for {FileName}", fileName);

            var response = await _httpClient.GetAsync(DatasetPageUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch dataset page. Status: {StatusCode}", response.StatusCode);
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            // Pattern to find download link: /media/filer_public/[hash]/[fileName]
            // Example: /media/filer_public/b2/3d/b23d5d9d-7f07-49a5-9ad8-8ec8917cdf82/2024-10.csv
            var pattern = $@"/media/filer_public/[a-f0-9/\-]+/{Regex.Escape(fileName)}";
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var downloadPath = match.Value;
                var fullUrl = $"{BaseUrl}{downloadPath}";
                _logger.LogInformation("Found download URL for {FileName}: {Url}", fileName, fullUrl);
                return fullUrl;
            }

            _logger.LogWarning("Could not find download URL for {FileName} in dataset page", fileName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while fetching download URL for {FileName}", fileName);
            return null;
        }
    }
}
