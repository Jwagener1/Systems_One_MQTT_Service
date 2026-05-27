using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Systems_One_MQTT_Updater.Update;

public class UpdateDownloader
{
    private readonly HttpClient _http;
    private readonly UpdaterSettings _settings;
    private readonly ILogger<UpdateDownloader> _logger;

    public UpdateDownloader(IHttpClientFactory httpFactory, IOptions<UpdaterSettings> options, ILogger<UpdateDownloader> logger)
    {
        _http = httpFactory.CreateClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Systems-One-MQTT-Updater/1.0");
        _http.Timeout = TimeSpan.FromMinutes(10);
        _settings = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Downloads the ZIP from the manifest and verifies its SHA-256 hash.
    /// Returns the path to the verified ZIP file.
    /// Throws on download failure or hash mismatch.
    /// </summary>
    public async Task<string> DownloadAndVerifyAsync(ReleaseManifest manifest, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_settings.DownloadsDir);

        var fileName = Path.GetFileName(new Uri(manifest.DownloadUrl).LocalPath);
        var destPath = Path.Combine(_settings.DownloadsDir, fileName);

        _logger.LogInformation("Downloading update {Version} from {Url}", manifest.Version, manifest.DownloadUrl);

        await DownloadFileAsync(manifest.DownloadUrl, destPath, cancellationToken);

        _logger.LogDebug("Download complete — verifying SHA-256");
        VerifyHash(destPath, manifest.Sha256);

        _logger.LogInformation("SHA-256 verified for {File}", fileName);
        return destPath;
    }

    private async Task DownloadFileAsync(string url, string destPath, CancellationToken cancellationToken)
    {
        // Delete any previous partial download
        if (File.Exists(destPath))
            File.Delete(destPath);

        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var src = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var dst = File.Create(destPath);

        var buffer = new byte[81920];
        long downloaded = 0;
        int read;

        while ((read = await src.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            downloaded += read;

            if (total.HasValue)
                _logger.LogTrace("Download progress: {Downloaded}/{Total} bytes", downloaded, total.Value);
        }

        _logger.LogDebug("Downloaded {Bytes} bytes to {Path}", downloaded, destPath);
    }

    private static void VerifyHash(string filePath, string expectedHex)
    {
        string actual;
        using (var sha256 = SHA256.Create())
        using (var stream = File.OpenRead(filePath))
        {
            actual = Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
        }

        if (!string.Equals(actual, expectedHex.ToLowerInvariant(), StringComparison.Ordinal))
        {
            File.Delete(filePath);
            throw new InvalidDataException(
                $"SHA-256 mismatch. Expected: {expectedHex}  Actual: {actual}");
        }
    }
}
