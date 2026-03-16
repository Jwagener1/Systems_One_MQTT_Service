using Systems_One_MQTT_Service.Abstractions;

namespace Systems_One_MQTT_Service.Infrastructure;

/// <summary>
/// Scheduler that aligns execution to clock boundaries (e.g., :00, :05, :10).
/// </summary>
public class IntervalScheduler : IScheduler
{
    private readonly IClock _clock;
    private readonly ILogger<IntervalScheduler> _logger;

    public IntervalScheduler(IClock clock, ILogger<IntervalScheduler> logger)
    {
        _clock = clock;
        _logger = logger;
    }

    public async Task ScheduleAsync(Func<CancellationToken, Task> action, TimeSpan interval, CancellationToken cancellationToken = default)
    {
        // Wait until the next clock-aligned boundary before starting
        var initialDelay = GetDelayUntilNextBoundary(interval);
        _logger.LogInformation(
            "Scheduler waiting {DelayMs}ms until next {IntervalMin}-minute boundary",
            (int)initialDelay.TotalMilliseconds, interval.TotalMinutes);

        await Task.Delay(initialDelay, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var tickStart = _clock.UtcNow;
            try
            {
                await action(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled action failed");
            }

            // Calculate delay to next boundary, accounting for execution time
            var elapsed = _clock.UtcNow - tickStart;
            var nextDelay = interval - TimeSpan.FromMilliseconds(elapsed.TotalMilliseconds % interval.TotalMilliseconds);
            if (nextDelay < TimeSpan.FromSeconds(1))
                nextDelay += interval;

            await Task.Delay(nextDelay, cancellationToken);
        }
    }

    public Task ScheduleAsync(Func<CancellationToken, Task> action, string cronExpression, CancellationToken cancellationToken = default)
    {
        // Cron not implemented — fallback to 1-minute aligned interval
        _logger.LogWarning("Cron scheduling not implemented, falling back to 1-minute interval");
        return ScheduleAsync(action, TimeSpan.FromMinutes(1), cancellationToken);
    }

    private TimeSpan GetDelayUntilNextBoundary(TimeSpan interval)
    {
        var now = _clock.UtcNow;
        var totalMs = interval.TotalMilliseconds;
        var msSinceEpoch = now.ToUnixTimeMilliseconds();
        var msIntoBucket = msSinceEpoch % (long)totalMs;
        var msUntilNext = (long)totalMs - msIntoBucket;
        return TimeSpan.FromMilliseconds(msUntilNext);
    }
}
