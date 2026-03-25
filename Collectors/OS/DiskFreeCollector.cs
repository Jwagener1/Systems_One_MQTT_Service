using Microsoft.Extensions.Options;
using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.OS;

/// <summary>
/// Collects disk free space information for internal/fixed drives only.
/// </summary>
public class DiskFreeCollector : IMetricCollector
{
    private readonly ILogger<DiskFreeCollector>? _logger;
    private readonly IClock _clock;
    private readonly HashSet<string>? _driveLetters;

    public string Name => "Disk Free";
    public string Category => "OS";

    public DiskFreeCollector(IClock clock, IOptions<DiskFreeCollectorOptions> options, ILogger<DiskFreeCollector>? logger = null)
    {
        _clock = clock;
        _logger = logger;

        var paths = options.Value.Monitors
            .Select(m => m.Path)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .ToList();

        if (paths.Count > 0)
        {
            _driveLetters = new HashSet<string>(
                paths.Select(NormalizeDriveLetter),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    public Task<IEnumerable<Metric>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var metrics = new List<Metric>();

        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed);

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
                Timestamp = _clock.UtcNow,
                Tags = new Dictionary<string, object>
                {
                    { "drive_count", list.Count },
                    { "filter", "internal_only" }
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to collect disk space metrics");
        }

        return Task.FromResult<IEnumerable<Metric>>(metrics);
    }

    internal static string NormalizeDriveLetter(string value)
    {
        value = value.Trim();
        if (OperatingSystem.IsWindows())
        {
            if (value.Length >= 1)
            {
                var letter = char.ToUpperInvariant(value[0]);
                return letter + ":";
            }
        }
        return value.TrimEnd('\\', '/');
    }
}
