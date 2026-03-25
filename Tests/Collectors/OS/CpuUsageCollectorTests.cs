using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Systems_One_MQTT_Service.Collectors.OS;
using Systems_One_MQTT_Service.Tests.Fakes;

namespace Systems_One_MQTT_Service.Tests.Collectors.OS;

public class CpuUsageCollectorTests
{
    private readonly FakeClock _clock = new();
    private readonly CpuUsageCollector _collector;

    public CpuUsageCollectorTests()
    {
        _collector = new CpuUsageCollector(_clock, NullLogger<CpuUsageCollector>.Instance);
    }

    [Fact]
    public async Task CollectAsync_ReturnsSingleMetric()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics.Should().HaveCount(1);
    }

    [Fact]
    public async Task CollectAsync_MetricIdIsCpuUsage()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics[0].Id.Should().Be("cpu.usage");
    }

    [Fact]
    public async Task CollectAsync_UnitIsPercent()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics[0].Unit.Should().Be("percent");
    }

    [Fact]
    public async Task CollectAsync_SourceIsOS()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics[0].Source.Should().Be("OS");
    }

    [Fact]
    public async Task CollectAsync_HasProcessorCountTag()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics[0].Tags.Should().ContainKey("processor_count");
    }

    [Fact]
    public void Category_IsOS()
    {
        _collector.Category.Should().Be("OS");
    }
}
