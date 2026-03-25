using Systems_One_MQTT_Service.Abstractions;

namespace Systems_One_MQTT_Service.Tests.Fakes;

public class FakeScheduler : IScheduler
{
    public int ExecutionCount { get; private set; }

    public async Task ScheduleAsync(Func<CancellationToken, Task> action, TimeSpan interval, CancellationToken ct = default)
    {
        ExecutionCount++;
        await action(ct);
    }

    public Task ScheduleAsync(Func<CancellationToken, Task> action, string cronExpression, CancellationToken ct = default)
        => ScheduleAsync(action, TimeSpan.FromMinutes(1), ct);
}
