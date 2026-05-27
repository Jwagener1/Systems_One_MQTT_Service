using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Systems_One_MQTT_Updater.GitHub;

public class ReleaseChecker
{
    private readonly HttpClient _http;
    private readonly UpdaterSettings _settings;
    private readonly ILogger<ReleaseChecker> _logger;

    // ETag from the last successful 200 response — sent as If-None-Match on the next request
    private string? _etag;

    public ReleaseChecker(HttpClient http, IOptions<UpdaterSettings> options, ILogger<ReleaseChecker> logger)
    {
        _http = http;
        _settings = options.Value;
        _logger = logger;

        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Systems-One-MQTT-Updater/1.0");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Fetches the release manifest and compares its version against the installed binary.
    /// Returns the manifest when a newer version is available, null otherwise.
    /// </summary>
    public async Task<ReleaseManifest?> CheckForUpdateAsync(CancellationToken cancellationToken)
    {
        var installedVersion = GetInstalledVersion();
        _logger.LogDebug("Checking for updates — installed version: {Version}", installedVersion);

        ReleaseManifest? manifest;
        try
        {
            manifest = await FetchManifestAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch release manifest from {Url}", _settings.ManifestUrl);
            return null;
        }

        if (manifest is null)
        {
            _logger.LogDebug("No new release (304 Not Modified or empty manifest)");
            return null;
        }

        if (!IsNewer(manifest.Version, installedVersion))
        {
            _logger.LogDebug("Manifest version {ManifestVersion} is not newer than installed {InstalledVersion}",
                manifest.Version, installedVersion);
            return null;
        }

        _logger.LogInformation("Update available: {InstalledVersion} → {ManifestVersion}", installedVersion, manifest.Version);
        return manifest;
    }

    private async Task<ReleaseManifest?> FetchManifestAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _settings.ManifestUrl);

        if (!string.IsNullOrEmpty(_etag))
            request.Headers.TryAddWithoutValidation("If-None-Match", _etag);

        using var response = await _http.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotModified)
            return null;

        response.EnsureSuccessStatusCode();

        // Cache the ETag for future conditional requests
        if (response.Headers.ETag is not null)
            _etag = response.Headers.ETag.ToString();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<ReleaseManifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private string GetInstalledVersion()
    {
        var exePath = Path.Combine(_settings.MainServiceInstallDir, "Systems_One_MQTT_Service.exe");
        if (!File.Exists(exePath))
        {
            _logger.LogWarning("Main service exe not found at {Path} — treating installed version as 0.0.0.0", exePath);
            return "0.0.0.0";
        }

        var info = FileVersionInfo.GetVersionInfo(exePath);
        return info.FileVersion ?? "0.0.0.0";
    }

    /// <summary>
    /// Compares two version strings in yyyy.MM.dd.build format.
    /// Returns true when candidate is strictly greater than installed.
    /// Unparseable candidate → false (never update to a version we can't read).
    /// Unparseable installed → treat as 0.0.0.0 (always update).
    /// </summary>
    public static bool IsNewer(string candidate, string installed)
    {
        if (!Version.TryParse(candidate, out var c))
            return false;

        if (!Version.TryParse(installed, out var i))
            i = new Version(0, 0, 0, 0);

        return c > i;
    }
}
