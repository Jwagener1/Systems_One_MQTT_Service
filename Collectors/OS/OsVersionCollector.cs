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

    public Task<IEnumerable<Metric>> CollectAsync(CancellationToken cancellationToken = default)
    {
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

        return Task.FromResult<IEnumerable<Metric>>(metrics);
    }
}
