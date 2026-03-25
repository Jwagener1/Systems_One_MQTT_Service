using FluentAssertions;
using Systems_One_MQTT_Service.Infrastructure;

namespace Systems_One_MQTT_Service.Tests.Infrastructure;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_IsCloseToCurrentTime()
    {
        var clock = new SystemClock();
        var now = DateTimeOffset.UtcNow;
        clock.UtcNow.Should().BeCloseTo(now, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void UtcNow_OffsetIsZero()
    {
        var clock = new SystemClock();
        clock.UtcNow.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Now_IsCloseToLocalTime()
    {
        var clock = new SystemClock();
        var now = DateTimeOffset.Now;
        clock.Now.Should().BeCloseTo(now, TimeSpan.FromSeconds(2));
    }
}
