namespace Systems_One_MQTT_Service.Abstractions;

/// <summary>
/// Defines a contract for publishing metrics to external systems.
/// </summary>
public interface IMetricPublisher
{
    /// <summary>
    /// Publishes a collection of metrics asynchronously.
    /// </summary>
    /// <param name="metrics">The metrics to publish.</param>
    /// <param name="cancellationToken">Cancellation token to stop the publishing process.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes the publisher connection asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the initialization.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects and cleans up resources asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the disconnection.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
