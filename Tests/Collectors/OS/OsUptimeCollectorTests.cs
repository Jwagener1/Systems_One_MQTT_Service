using FluentAssertions;
using Systems_One_MQTT_Service.Collectors.OS;
using Systems_One_MQTT_Service.Tests.Fakes;

namespace Systems_One_MQTT_Service.Tests.Collectors.OS;

public class OsUptimeCollectorTests
{
    private readonly FakeClock _clock = new();
    private readonly OsUptimeCollector _collector;

    public OsUptimeCollectorTests()
    {
        _collector = new OsUptimeCollector(_clock);
    }

    [Fact]
    public async Task CollectAsync_ReturnsSingleMetric()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics.Should().HaveCount(1);
    }

    [Fact]
    public async Task CollectAsync_MetricIdIsOsUptime()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics[0].Id.Should().Be("os.uptime");
    }

    [Fact]
    public async Task CollectAsync_ValueIsPositive()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        Convert.ToDouble(metrics[0].Value).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CollectAsync_UnitIsSeconds()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics[0].Unit.Should().Be("seconds");
    }

    [Fact]
    public async Task CollectAsync_HasUptimeTags()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics[0].Tags.Should().ContainKey("uptime_days");
        metrics[0].Tags.Should().ContainKey("uptime_hours");
        metrics[0].Tags.Should().ContainKey("uptime_minutes");
    }

    [Fact]
    public async Task CollectAsync_UsesInjectedClock()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics[0].Timestamp.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public void Category_IsOS()
    {
        _collector.Category.Should().Be("OS");
    }
}
