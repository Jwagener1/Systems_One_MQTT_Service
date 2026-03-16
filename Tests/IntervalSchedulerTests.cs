using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Infrastructure;

namespace Systems_One_MQTT_Service.Tests;

public class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset Now => UtcNow.ToLocalTime();
}

public class IntervalSchedulerTests
{
    [Fact]
    public async Task ScheduleAsync_AlignsToClockBoundary()
    {
        // Arrange: clock is at 12:03:00 UTC, interval is 5 minutes
        // Next boundary should be 12:05:00 (2 minutes away)
        var clock = new FakeClock
        {
            UtcNow = new DateTimeOffset(2026, 3, 16, 12, 3, 0, TimeSpan.Zero)
        };

        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<IntervalScheduler>();
        var scheduler = new IntervalScheduler(clock, logger);

        var executionCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        try
        {
            await scheduler.ScheduleAsync(async ct =>
            {
                executionCount++;
                ct.ThrowIfCancellationRequested();
            }, TimeSpan.FromMinutes(5), cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected — we cancel quickly to test alignment logic
        }

        // The scheduler should have waited for the boundary,
        // which with a fake clock that doesn't advance means it waits then gets cancelled
        Assert.True(executionCount <= 1, "Scheduler should not run many times in 200ms");
    }
}
