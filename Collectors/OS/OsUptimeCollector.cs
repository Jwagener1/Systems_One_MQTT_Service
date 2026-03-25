using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.OS;

/// <summary>
/// Collects operating system uptime information.
/// </summary>
public class OsUptimeCollector : IMetricCollector
{
    private readonly IClock _clock;
    private readonly ILogger<OsUptimeCollector>? _logger;

    public string Name => "OS Uptime";
    public string Category => "OS";

    public OsUptimeCollector(IClock clock, ILogger<OsUptimeCollector>? logger = null)
    {
        _clock = clock;
        _logger = logger;
    }

    public Task<IEnumerable<Metric>> CollectAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogTrace("OsUptimeCollector.CollectAsync started");
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
            Timestamp = _clock.UtcNow,
            Tags = new Dictionary<string, object>
            {
                { "uptime_days", uptimeTimeSpan.Days },
                { "uptime_hours", uptimeTimeSpan.Hours },
                { "uptime_minutes", uptimeTimeSpan.Minutes }
            }
        });

        _logger?.LogDebug("Uptime collected: {Days}d {Hours}h {Minutes}m", uptimeTimeSpan.Days, uptimeTimeSpan.Hours, uptimeTimeSpan.Minutes);
        return Task.FromResult<IEnumerable<Metric>>(metrics);
    }
}
