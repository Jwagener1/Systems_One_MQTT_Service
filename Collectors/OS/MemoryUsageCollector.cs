using System.Diagnostics;
using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.OS;

/// <summary>
/// Collects memory usage information.
/// </summary>
public class MemoryUsageCollector : IMetricCollector, IDisposable
{
    private readonly PerformanceCounter? _availableMemoryCounter;
    private readonly ILogger<MemoryUsageCollector>? _logger;
    private readonly IClock _clock;

    public string Name => "Memory Usage";
    public string Category => "OS";

    public MemoryUsageCollector(IClock clock, ILogger<MemoryUsageCollector>? logger = null)
    {
        _clock = clock;
        _logger = logger;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                _availableMemoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initialize memory performance counter");
        }
    }

    public Task<IEnumerable<Metric>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var metrics = new List<Metric>();

        try
        {
            var gcInfo = GC.GetGCMemoryInfo();
            var totalMemoryBytes = gcInfo.TotalAvailableMemoryBytes;
            var totalMemoryMB = totalMemoryBytes / 1024.0 / 1024.0;

            double availableMemoryMB;
            if (_availableMemoryCounter != null && OperatingSystem.IsWindows())
            {
                availableMemoryMB = _availableMemoryCounter.NextValue();
            }
            else
            {
                availableMemoryMB = (totalMemoryBytes - gcInfo.MemoryLoadBytes) / 1024.0 / 1024.0;
            }

            var usedMemoryMB = totalMemoryMB - availableMemoryMB;
            var memoryUsagePercent = (usedMemoryMB / totalMemoryMB) * 100;
            var now = _clock.UtcNow;

            metrics.Add(new Metric
            {
                Id = "memory.total",
                Name = "Total Memory",
                Value = Math.Round(totalMemoryMB, 2),
                Unit = "MB",
                Source = "OS",
                Timestamp = now
            });

            metrics.Add(new Metric
            {
                Id = "memory.available",
                Name = "Available Memory",
                Value = Math.Round(availableMemoryMB, 2),
                Unit = "MB",
                Source = "OS",
                Timestamp = now
            });

            metrics.Add(new Metric
            {
                Id = "memory.used",
                Name = "Used Memory",
                Value = Math.Round(usedMemoryMB, 2),
                Unit = "MB",
                Source = "OS",
                Timestamp = now
            });

            metrics.Add(new Metric
            {
                Id = "memory.usage",
                Name = "Memory Usage",
                Value = Math.Round(memoryUsagePercent, 2),
                Unit = "percent",
                Source = "OS",
                Timestamp = now
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to collect memory usage metrics");
        }

        return Task.FromResult<IEnumerable<Metric>>(metrics);
    }

    public void Dispose()
    {
        _availableMemoryCounter?.Dispose();
    }
}
