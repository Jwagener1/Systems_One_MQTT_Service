namespace Systems_One_MQTT_Service.Metrics;

/// <summary>
/// Represents a metric data point collected from a source.
/// </summary>
public class Metric
{
    /// <summary>
    /// Gets or sets the unique identifier for the metric.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the metric.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the value of the metric.
    /// </summary>
    public object Value { get; set; } = null!;

    /// <summary>
    /// Gets or sets the unit of measurement for the metric.
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the metric was collected.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the source of the metric (e.g., "OS", "App", "DB").
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional metadata for the metric.
    /// </summary>
    public Dictionary<string, object>? Tags { get; set; }
}
