using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Systems_One_MQTT_Service.Collectors.OS;
using Systems_One_MQTT_Service.Tests.Fakes;

namespace Systems_One_MQTT_Service.Tests.Collectors.OS;

public class OsVersionCollectorTests
{
    private readonly FakeClock _clock = new();
    private readonly OsVersionCollector _collector;

    public OsVersionCollectorTests()
    {
        _collector = new OsVersionCollector(NullLogger<OsVersionCollector>.Instance, _clock);
    }

    [Fact]
    public async Task CollectAsync_ReturnsSingleMetric()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics.Should().HaveCount(1);
    }

    [Fact]
    public async Task CollectAsync_MetricIdIsOsVersion()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics[0].Id.Should().Be("os.version");
    }

    [Fact]
    public async Task CollectAsync_ValueIsNotEmpty()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics[0].Value.Should().NotBeNull();
        metrics[0].Value.ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CollectAsync_SourceIsOS()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics[0].Source.Should().Be("OS");
    }

    [Fact]
    public async Task CollectAsync_HasPlatformTag()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics[0].Tags.Should().ContainKey("platform");
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
