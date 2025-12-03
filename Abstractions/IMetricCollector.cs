using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Abstractions;

/// <summary>
/// Defines a contract for collecting metrics from various sources.
/// </summary>
public interface IMetricCollector
{
    /// <summary>
    /// Collects metrics asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the collection process.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IEnumerable<Metric>> CollectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the name of the collector for identification purposes.
    /// </summary>
    string Name { get; }
}
