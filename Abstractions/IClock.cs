namespace Systems_One_MQTT_Service.Abstractions;

/// <summary>
/// Defines a contract for providing current time, enabling testability of time-based behavior.
/// </summary>
public interface IClock
{
    /// <summary>
    /// Gets the current date and time in UTC.
    /// </summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Gets the current date and time in local time.
    /// </summary>
    DateTimeOffset Now { get; }
}
