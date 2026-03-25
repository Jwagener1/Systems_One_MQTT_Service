using Systems_One_MQTT_Service.Abstractions;

namespace Systems_One_MQTT_Service.Tests.Fakes;

public class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    public DateTimeOffset Now => UtcNow.ToLocalTime();
    public void Advance(TimeSpan duration) => UtcNow = UtcNow.Add(duration);
}
