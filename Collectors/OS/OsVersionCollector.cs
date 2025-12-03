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
    private readonly ILogger<OsVersionCollector> _logger;

    public OsVersionCollector(ILogger<OsVersionCollector> logger)
    {
        _logger = logger;
    }

    public Task<IEnumerable<Metric>> CollectAsync(CancellationToken cancellationToken = default)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["Component"] = nameof(OsVersionCollector) }))
        {
            _logger.LogDebug("Collecting OS version metrics");
            var metrics = new List<Metric>();

            var osVersion = Environment.OSVersion;
            var osDescription = RuntimeInformation.OSDescription;
            var osArchitecture = RuntimeInformation.OSArchitecture.ToString();

            metrics.Add(new Metric
            {
                Id = "os.version",
                Name = "Operating System Version",
                Value = osVersion.VersionString,
                Source = "OS",
                Timestamp = DateTimeOffset.UtcNow,
                Tags = new Dictionary<string, object>
                {
                    { "platform", osVersion.Platform.ToString() },
                    { "version_major", osVersion.Version.Major },
                    { "version_minor", osVersion.Version.Minor },
                    { "version_build", osVersion.Version.Build }
                }
            });

            metrics.Add(new Metric
            {
                Id = "os.description",
                Name = "Operating System Description",
                Value = osDescription,
                Source = "OS",
                Timestamp = DateTimeOffset.UtcNow,
                Tags = new Dictionary<string, object>
                {
                    { "architecture", osArchitecture }
                }
            });

            _logger.LogDebug("Collected {Count} OS metrics", metrics.Count);
            return Task.FromResult<IEnumerable<Metric>>(metrics);
        }
    }
}
