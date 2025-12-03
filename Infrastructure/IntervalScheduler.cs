using Systems_One_MQTT_Service.Abstractions;

namespace Systems_One_MQTT_Service.Infrastructure;

public class IntervalScheduler : IScheduler
{
    public async Task ScheduleAsync(Func<CancellationToken, Task> action, TimeSpan interval, CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await action(cancellationToken);
            }
            catch
            {
            }

            await Task.Delay(interval, cancellationToken);
        }
    }

    public Task ScheduleAsync(Func<CancellationToken, Task> action, string cronExpression, CancellationToken cancellationToken = default)
    {
        // Cron scheduling can be implemented later; for now, fallback to 1-minute interval
        return ScheduleAsync(action, TimeSpan.FromMinutes(1), cancellationToken);
    }
}