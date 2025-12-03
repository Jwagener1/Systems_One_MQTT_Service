using System.Diagnostics;
using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.OS;

/// <summary>
/// Collects operating system uptime information.
/// </summary>
public class OsUptimeCollector : IMetricCollector
{
    public string Name => "OS Uptime";

    public Task<IEnumerable<Metric>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var metrics = new List<Metric>();

        var uptimeMilliseconds = Environment.TickCount64;
        var uptimeTimeSpan = TimeSpan.FromMilliseconds(uptimeMilliseconds);

        metrics.Add(new Metric
        {
            Id = "os.uptime",
            Name = "Operating System Uptime",
            Value = uptimeTimeSpan.TotalSeconds,
            Unit = "seconds",
            Source = "OS",
            Timestamp = DateTimeOffset.UtcNow,
            Tags = new Dictionary<string, object>
            {
                { "uptime_days", uptimeTimeSpan.Days },
                { "uptime_hours", uptimeTimeSpan.Hours },
                { "uptime_minutes", uptimeTimeSpan.Minutes }
            }
        });

        return Task.FromResult<IEnumerable<Metric>>(metrics);
    }
}
