using System.Net;
using System.Text.RegularExpressions;
using Electricity.Application.Interfaces;
using Electricity.Infrastructure.Configuration;
using Electricity.Infrastructure.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Electricity.Infrastructure.ExternalServices;

public class DataGovLtDataSourceRepository : IDataSourceRepository
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DataGovLtDataSourceRepository> _logger;
    private readonly DownloadOptions _options;

    private const string DatasetPageUrl = "https://data.gov.lt/datasets/1975/";
    private const string BaseUrl = "https://data.gov.lt";

    private static readonly HashSet<HttpStatusCode> RetryableStatusCodes = new()
    {
        HttpStatusCode.RequestTimeout,
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout
    };

    public DataGovLtDataSourceRepository(
        HttpClient httpClient,
        IOptions<DownloadOptions> options,
        ILogger<DataGovLtDataSourceRepository> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<Stream> DownloadCsvFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var downloadUrl = await GetDownloadUrlWithRetryAsync(fileName, cancellationToken);
        if (string.IsNullOrEmpty(downloadUrl))
        {
            throw new InvalidOperationException($"Could not find download URL for file {fileName}");
        }

        return await DownloadWithRetryAsync(downloadUrl, fileName, cancellationToken);
    }

    private async Task<string?> GetDownloadUrlWithRetryAsync(string fileName, CancellationToken cancellationToken)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _options.MaxRetries)
        {
            attempt++;

            try
            {
                var result = await TryGetDownloadUrlAsync(fileName, attempt, cancellationToken);
                if (result is not null)
                {
                    return result;
                }
            }
            catch (DownloadException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "URL resolution attempt {Attempt} failed for {FileName}", attempt, fileName);
            }

            if (attempt < _options.MaxRetries)
            {
                await DelayBeforeRetryAsync(attempt, cancellationToken);
            }
        }

        _logger.LogError(lastException, "Failed to resolve download URL for {FileName} after {MaxRetries} attempts",
            fileName, _options.MaxRetries);
        return null;
    }

    private async Task<string?> TryGetDownloadUrlAsync(string fileName, int attempt, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching dataset page to find download URL for {FileName} (attempt {Attempt}/{Max})",
            fileName, attempt, _options.MaxRetries);

        var response = await _httpClient.GetAsync(DatasetPageUrl, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;

            if (IsRetryableStatusCode(statusCode))
            {
                _logger.LogWarning("Dataset page request failed with retryable status: {StatusCode}", statusCode);
                return null; // Signal retry
            }

            throw new DownloadException($"Failed to fetch dataset page with non-retryable status: {statusCode}");
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

        _logger.LogWarning("Could not find download URL for {FileName} in dataset page HTML", fileName);
        throw new DownloadException($"Download URL not found in dataset page for {fileName}");
    }

    private async Task<Stream> DownloadWithRetryAsync(
        string url,
        string fileName,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt < _options.MaxRetries)
        {
            attempt++;

            try
            {
                var result = await TryDownloadAsync(url, fileName, attempt, cancellationToken);
                if (result is not null)
                {
                    return result;
                }
            }
            catch (DownloadException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Download attempt {Attempt} failed for {FileName}", attempt, fileName);
            }

            if (attempt < _options.MaxRetries)
            {
                await DelayBeforeRetryAsync(attempt, cancellationToken);
            }
        }

        throw new DownloadException(
            $"Failed to download {fileName} after {_options.MaxRetries} attempts",
            lastException);
    }

    private async Task<MemoryStream?> TryDownloadAsync(
        string url,
        string fileName,
        int attempt,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Downloading {FileName} (attempt {Attempt}/{Max})",
            fileName, attempt, _options.MaxRetries);

        using var response = await _httpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Successfully downloaded CSV file {FileName}", fileName);
            return await CopyToMemoryStreamAsync(response, cancellationToken);
        }

        LogDownloadFailure(fileName, response.StatusCode);

        if (!IsRetryableStatusCode(response.StatusCode))
        {
            throw new DownloadException(
                $"Download failed with non-retryable status: {response.StatusCode}");
        }

        return null;
    }

    private async Task<MemoryStream> CopyToMemoryStreamAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var memoryStream = new MemoryStream();

        try
        {
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await contentStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch
        {
            await memoryStream.DisposeAsync();
            throw;
        }
    }

    private async Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var delay = CalculateBackoffDelay(attempt);
        _logger.LogInformation("Retrying in {Delay:F1} seconds...", delay.TotalSeconds);
        await Task.Delay(delay, cancellationToken);
    }

    private TimeSpan CalculateBackoffDelay(int attempt)
    {
        var baseDelay = Math.Pow(2, attempt);
        var jitter = Random.Shared.NextDouble() * 0.5;
        var totalSeconds = baseDelay * (1 + jitter);

        return TimeSpan.FromSeconds(Math.Min(totalSeconds, _options.MaxDelaySeconds));
    }

    private static bool IsRetryableStatusCode(HttpStatusCode statusCode) =>
        RetryableStatusCodes.Contains(statusCode);

    private void LogDownloadFailure(string fileName, HttpStatusCode statusCode)
    {
        if (IsRetryableStatusCode(statusCode))
        {
            _logger.LogWarning(
                "Download of {FileName} failed with retryable status: {StatusCode}",
                fileName, statusCode);
        }
        else
        {
            _logger.LogError(
                "Download of {FileName} failed with non-retryable status: {StatusCode}",
                fileName, statusCode);
        }
    }
}
