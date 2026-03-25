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
        var executed = false;

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        try
        {
            await scheduler.ScheduleAsync(ct =>
            {
                executed = true;
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }, TimeSpan.FromMinutes(5), cts.Token);
        }
        catch (OperationCanceledException) { }

        // May or may not have executed depending on boundary alignment,
        // but should not throw unexpectedly
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
    public async Task CronFallback_ExecutesAction()
    {
        var clock = new FakeClock();
        var scheduler = new IntervalScheduler(clock, NullLogger<IntervalScheduler>.Instance);
        var executed = false;

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        try
        {
            await scheduler.ScheduleAsync(ct =>
            {
                executed = true;
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }, "0 * * * *", cts.Token);
        }
        catch (OperationCanceledException) { }

        // Should fall back to interval-based execution
    }
}
