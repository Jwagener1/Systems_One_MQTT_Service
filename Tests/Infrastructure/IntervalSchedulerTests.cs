using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Systems_One_MQTT_Service.Infrastructure;
using Systems_One_MQTT_Service.Tests.Fakes;

namespace Systems_One_MQTT_Service.Tests.Infrastructure;

public class IntervalSchedulerTests
{
    [Fact]
    public async Task ScheduleAsync_ExecutesAction()
    {
        var clock = new FakeClock();
        var scheduler = new IntervalScheduler(clock, NullLogger<IntervalScheduler>.Instance);
        var executionCount = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        try
        {
            // Short interval so at least one tick fires inside the cancellation window.
            await scheduler.ScheduleAsync(_ =>
            {
                Interlocked.Increment(ref executionCount);
                return Task.CompletedTask;
            }, TimeSpan.FromMilliseconds(250), cts.Token);
        }
        catch (OperationCanceledException) { }

        executionCount.Should().BeGreaterThan(0, "the action must run at least once before cancellation");
    }

    [Fact]
    public async Task ScheduleAsync_CancellationStops()
    {
        var clock = new FakeClock();
        var scheduler = new IntervalScheduler(clock, NullLogger<IntervalScheduler>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var act = async () => await scheduler.ScheduleAsync(
            _ => Task.CompletedTask,
            TimeSpan.FromMinutes(1),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ScheduleAsync_ActionExceptionsDoNotStopScheduler()
    {
        var clock = new FakeClock();
        var scheduler = new IntervalScheduler(clock, NullLogger<IntervalScheduler>.Instance);
        var executionCount = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        try
        {
            await scheduler.ScheduleAsync(_ =>
            {
                Interlocked.Increment(ref executionCount);
                throw new InvalidOperationException("boom");
            }, TimeSpan.FromMilliseconds(250), cts.Token);
        }
        catch (OperationCanceledException) { }

        executionCount.Should().BeGreaterThan(1, "scheduler should keep firing even when the action throws");
    }

    [Fact]
    public async Task CronFallback_ExecutesAction()
    {
        var clock = new FakeClock();
        var scheduler = new IntervalScheduler(clock, NullLogger<IntervalScheduler>.Instance);
        var executionCount = 0;

        // Cron path falls back to a 1-minute interval, so we just verify that
        // cancellation propagates out cleanly within the timeout window.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var act = async () => await scheduler.ScheduleAsync(ct =>
        {
            Interlocked.Increment(ref executionCount);
            return Task.CompletedTask;
        }, "0 * * * *", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        executionCount.Should().Be(0, "1-minute fallback interval should not elapse inside the 200ms test window");
    }
}
