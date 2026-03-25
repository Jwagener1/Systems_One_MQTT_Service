using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Systems_One_MQTT_Service.Collectors.OS;
using Systems_One_MQTT_Service.Tests.Fakes;

namespace Systems_One_MQTT_Service.Tests.Collectors.OS;

public class MemoryUsageCollectorTests
{
    private readonly FakeClock _clock = new();
    private readonly MemoryUsageCollector _collector;

    public MemoryUsageCollectorTests()
    {
        _collector = new MemoryUsageCollector(_clock, NullLogger<MemoryUsageCollector>.Instance);
    }

    [Fact]
    public async Task CollectAsync_ReturnsFourMetrics()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics.Should().HaveCount(4);
    }

    [Fact]
    public async Task CollectAsync_ContainsExpectedMetricIds()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        var ids = metrics.Select(m => m.Id).ToList();
        ids.Should().Contain("memory.total");
        ids.Should().Contain("memory.available");
        ids.Should().Contain("memory.used");
        ids.Should().Contain("memory.usage");
    }

    [Fact]
    public async Task CollectAsync_AllValuesAreNonNegative()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        foreach (var m in metrics)
        {
            Convert.ToDouble(m.Value).Should().BeGreaterOrEqualTo(0);
        }
    }

    [Fact]
    public void Category_IsOS()
    {
        _collector.Category.Should().Be("OS");
    }

    [Fact]
    public async Task CollectAsync_UsesInjectedClock()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics.Should().OnlyContain(m => m.Timestamp == _clock.UtcNow);
    }
}
