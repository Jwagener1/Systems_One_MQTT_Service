using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.OS;

/// <summary>
/// Collects disk free space information.
/// </summary>
public class DiskFreeCollector : IMetricCollector
{
    private readonly ILogger<DiskFreeCollector>? _logger;
    private readonly HashSet<string>? _driveLetters; // normalized like "C:" or "/" on Unix

    public string Name => "OS";

    /// <summary>
    /// Create a DiskFreeCollector.
    /// Provide specific drive letters to restrict monitoring to those drives.
    /// Examples on Windows: ["C", "D"], they will be normalized to "C:" and "D:".
    /// On Unix-like systems, use mount names (e.g., "/").
    /// </summary>
    public DiskFreeCollector(ILogger<DiskFreeCollector>? logger = null, IEnumerable<string>? driveLetters = null)
    {
        _logger = logger;
        if (driveLetters != null)
        {
            _driveLetters = new HashSet<string>(driveLetters
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(NormalizeDriveLetter), StringComparer.OrdinalIgnoreCase);
        }
    }

    public Task<IEnumerable<Metric>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var metrics = new List<Metric>();

        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed);

            // If specific drive letters provided, filter to those
            if (_driveLetters != null && _driveLetters.Count > 0)
            {
                drives = drives.Where(d => _driveLetters.Contains(NormalizeDriveLetter(d.Name)));
            }

            var list = new List<object>();
            foreach (var drive in drives)
            {
                try
                {
                    var totalSpaceGB = drive.TotalSize / 1024.0 / 1024.0 / 1024.0;
                    var freeSpaceGB = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
                    var usedSpaceGB = totalSpaceGB - freeSpaceGB;
                    var usagePercent = (usedSpaceGB / totalSpaceGB) * 100;

                    var driveName = drive.Name.TrimEnd('\\', '/');

                    list.Add(new
                    {
                        drive = driveName,
                        totalGB = Math.Round(totalSpaceGB, 2),
                        freeGB = Math.Round(freeSpaceGB, 2),
                        usedGB = Math.Round(usedSpaceGB, 2),
                        usagePercent = Math.Round(usagePercent, 2),
                        driveType = drive.DriveType.ToString(),
                        format = drive.DriveFormat
                    });
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to collect metrics for drive {DriveName}", drive.Name);
                }
            }

            metrics.Add(new Metric
            {
                Id = "os.drives",
                Name = "Operating System Drives",
                Value = list,
                Source = "OS",
                Timestamp = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to collect disk space metrics");
        }

        return Task.FromResult<IEnumerable<Metric>>(metrics);
    }

    private static string NormalizeDriveLetter(string value)
    {
        value = value.Trim();
        if (OperatingSystem.IsWindows())
        {
            // Accept formats like "C", "C:", "C:\", normalize to "C:"
            if (value.Length >= 1)
            {
                var letter = char.ToUpperInvariant(value[0]);
                return letter + ":";
            }
        }
        // For non-Windows, return as-is (e.g., "/")
        return value.TrimEnd('\\', '/');
    }
}
