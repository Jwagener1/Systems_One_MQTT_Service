namespace Systems_One_MQTT_Service.Abstractions;

/// <summary>
/// Defines a contract for scheduling metric collection operations.
/// </summary>
public interface IScheduler
{
    /// <summary>
    /// Schedules a task to run at specified intervals.
    /// </summary>
    /// <param name="action">The action to execute on each interval.</param>
    /// <param name="interval">The time interval between executions.</param>
    /// <param name="cancellationToken">Cancellation token to stop the scheduled task.</param>
    /// <returns>A task that represents the scheduled operation.</returns>
    Task ScheduleAsync(Func<CancellationToken, Task> action, TimeSpan interval, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a task to run using a cron expression.
    /// </summary>
    /// <param name="action">The action to execute according to the cron schedule.</param>
    /// <param name="cronExpression">The cron expression defining the schedule.</param>
    /// <param name="cancellationToken">Cancellation token to stop the scheduled task.</param>
    /// <returns>A task that represents the scheduled operation.</returns>
    Task ScheduleAsync(Func<CancellationToken, Task> action, string cronExpression, CancellationToken cancellationToken = default);
}
