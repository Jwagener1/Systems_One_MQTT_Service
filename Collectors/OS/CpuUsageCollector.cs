using System.Diagnostics;
using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.OS;

/// <summary>
/// Collects CPU usage information.
/// </summary>
public class CpuUsageCollector : IMetricCollector, IDisposable
{
    private readonly PerformanceCounter? _cpuCounter;
    private readonly ILogger<CpuUsageCollector>? _logger;
    private readonly IClock _clock;

    public string Name => "CPU Usage";
    public string Category => "OS";

    public CpuUsageCollector(IClock clock, ILogger<CpuUsageCollector>? logger = null)
    {
        _clock = clock;
        _logger = logger;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                // Initial call returns 0, so we call it once to initialize
                _cpuCounter.NextValue();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initialize CPU performance counter. CPU metrics will use Process-level data.");
        }
    }

    public async Task<IEnumerable<Metric>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var metrics = new List<Metric>();

        try
        {
            double cpuUsage;

            if (_cpuCounter != null && OperatingSystem.IsWindows())
            {
                cpuUsage = _cpuCounter.NextValue();
            }
            else
            {
                // Fallback to process CPU usage on non-Windows platforms
                var process = Process.GetCurrentProcess();
                var startTime = DateTime.UtcNow;
                var startCpuUsage = process.TotalProcessorTime;

                await Task.Delay(100, cancellationToken);

                var endTime = DateTime.UtcNow;
                var endCpuUsage = process.TotalProcessorTime;

                var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                cpuUsage = (cpuUsedMs / (Environment.ProcessorCount * totalMsPassed)) * 100;
            }

            metrics.Add(new Metric
            {
                Id = "cpu.usage",
                Name = "CPU Usage",
                Value = Math.Round(cpuUsage, 2),
                Unit = "percent",
                Source = "OS",
                Timestamp = _clock.UtcNow,
                Tags = new Dictionary<string, object>
                {
                    { "processor_count", Environment.ProcessorCount }
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to collect CPU usage metric");
        }

        return metrics;
    }

    public void Dispose()
    {
        _cpuCounter?.Dispose();
    }
}
