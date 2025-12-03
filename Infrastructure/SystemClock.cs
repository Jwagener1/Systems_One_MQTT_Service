using Systems_One_MQTT_Service.Abstractions;

namespace Systems_One_MQTT_Service.Infrastructure;

public class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public DateTimeOffset Now => DateTimeOffset.Now;
}