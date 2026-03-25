using System.Diagnostics;
using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.OS;

/// <summary>
/// Collects memory usage information as a single consolidated metric.
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
                _logger?.LogDebug("Memory performance counter initialized");
            }
            else
            {
                _logger?.LogDebug("Non-Windows platform: using GC memory info fallback");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initialize memory performance counter");
        }
    }

    public Task<IEnumerable<Metric>> CollectAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogTrace("MemoryUsageCollector.CollectAsync started");
        var metrics = new List<Metric>();

        try
        {
            var gcInfo = GC.GetGCMemoryInfo();
            var totalMemoryBytes = gcInfo.TotalAvailableMemoryBytes;
            var totalMemoryGB = totalMemoryBytes / 1024.0 / 1024.0 / 1024.0;

            double availableMemoryGB;
            if (_availableMemoryCounter != null && OperatingSystem.IsWindows())
            {
                availableMemoryGB = _availableMemoryCounter.NextValue() / 1024.0; // MBytes → GB
            }
            else
            {
                availableMemoryGB = (totalMemoryBytes - gcInfo.MemoryLoadBytes) / 1024.0 / 1024.0 / 1024.0;
            }

            var usedMemoryGB = totalMemoryGB - availableMemoryGB;
            var usagePercent = totalMemoryGB > 0 ? (usedMemoryGB / totalMemoryGB) * 100 : 0;

            _logger?.LogTrace("Memory raw: Total={TotalGB:F2}GB, Available={AvailGB:F2}GB, Used={UsedGB:F2}GB",
                totalMemoryGB, availableMemoryGB, usedMemoryGB);

            metrics.Add(new Metric
            {
                Id = "memory",
                Name = "Memory",
                Value = new
                {
                    totalGB = Math.Round(totalMemoryGB, 2),
                    freeGB = Math.Round(availableMemoryGB, 2),
                    usedGB = Math.Round(usedMemoryGB, 2),
                    usagePercent = Math.Round(usagePercent, 2)
                },
                Unit = "GB",
                Source = "OS",
                Timestamp = _clock.UtcNow
            });

            _logger?.LogDebug("Memory: {UsedGB:F1}GB / {TotalGB:F1}GB ({Usage:F1}%)",
                usedMemoryGB, totalMemoryGB, usagePercent);
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
