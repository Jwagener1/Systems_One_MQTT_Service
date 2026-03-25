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
    public async Task CollectAsync_ReturnsSingleMetric()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics.Should().HaveCount(1);
    }

    [Fact]
    public async Task CollectAsync_MetricIdIsMemory()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics[0].Id.Should().Be("memory");
    }

    [Fact]
    public async Task CollectAsync_ValueIsConsolidated()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        var value = metrics[0].Value;
        value.Should().NotBeNull();
        // Value should be an anonymous type with totalGB, freeGB, usedGB, usagePercent
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        json.Should().Contain("totalGB");
        json.Should().Contain("freeGB");
        json.Should().Contain("usedGB");
        json.Should().Contain("usagePercent");
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
