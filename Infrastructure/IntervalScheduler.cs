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
        var initialDelay = GetDelayUntilNextBoundary(interval);
        _logger.LogDebug("Scheduler: waiting {DelayMs}ms until next {IntervalMin}-min boundary",
            (int)initialDelay.TotalMilliseconds, interval.TotalMinutes);

        await Task.Delay(initialDelay, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var tickStart = _clock.UtcNow;
            _logger.LogTrace("Scheduler tick started at {TickStart}", tickStart);

            try
            {
                await action(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled action failed");
            }

            var elapsed = _clock.UtcNow - tickStart;
            var nextDelay = interval - TimeSpan.FromMilliseconds(elapsed.TotalMilliseconds % interval.TotalMilliseconds);
            if (nextDelay < TimeSpan.FromSeconds(1))
                nextDelay += interval;

            _logger.LogTrace("Scheduler: next tick in {DelayMs}ms (action took {ElapsedMs}ms)",
                (int)nextDelay.TotalMilliseconds, (int)elapsed.TotalMilliseconds);

            await Task.Delay(nextDelay, cancellationToken);
        }
    }

    public Task ScheduleAsync(Func<CancellationToken, Task> action, string cronExpression, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Cron scheduling not implemented — falling back to 1-minute interval");
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
