using System.Runtime.InteropServices;
using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.OS;

/// <summary>
/// Collects operating system version information.
/// </summary>
public class OsVersionCollector : IMetricCollector
{
    public string Name => "OS Version";
    public string Category => "OS";

    private readonly ILogger<OsVersionCollector> _logger;
    private readonly IClock _clock;

    public OsVersionCollector(ILogger<OsVersionCollector> logger, IClock clock)
    {
        _logger = logger;
        _clock = clock;
    }

    public Task<IEnumerable<Metric>> CollectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("OsVersionCollector.CollectAsync started");
        var metrics = new List<Metric>();

        var osVersion = Environment.OSVersion;

        metrics.Add(new Metric
        {
            Id = "os.version",
            Name = "Operating System Version",
            Value = osVersion.VersionString,
            Source = "OS",
            Timestamp = _clock.Now,
            Tags = new Dictionary<string, object>
            {
                { "platform", osVersion.Platform.ToString() },
                { "version_major", osVersion.Version.Major },
                { "version_minor", osVersion.Version.Minor },
                { "version_build", osVersion.Version.Build }
            }
        });

        _logger.LogDebug("OS version collected: {Version}", osVersion.VersionString);
        return Task.FromResult<IEnumerable<Metric>>(metrics);
    }
}
