using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Systems_One_MQTT_Service.Collectors.OS;
using Systems_One_MQTT_Service.Tests.Fakes;

namespace Systems_One_MQTT_Service.Tests.Collectors.OS;

public class DiskFreeCollectorTests
{
    private readonly FakeClock _clock = new();

    private DiskFreeCollector CreateCollector(DiskFreeCollectorOptions? opts = null)
    {
        return new DiskFreeCollector(
            _clock,
            Options.Create(opts ?? new DiskFreeCollectorOptions()),
            NullLogger<DiskFreeCollector>.Instance);
    }

    [Fact]
    public async Task CollectAsync_ReturnsOsDrivesMetric()
    {
        var collector = CreateCollector();
        var metrics = (await collector.CollectAsync()).ToList();
        metrics.Should().HaveCount(1);
        metrics[0].Id.Should().Be("os.drives");
    }

    [Fact]
    public async Task CollectAsync_SourceIsOS()
    {
        var collector = CreateCollector();
        var metrics = (await collector.CollectAsync()).ToList();
        metrics[0].Source.Should().Be("OS");
    }

    [Fact]
    public void Category_IsOS()
    {
        CreateCollector().Category.Should().Be("OS");
    }

    [Fact]
    public async Task CollectAsync_UsesInjectedClock()
    {
        var collector = CreateCollector();
        var metrics = (await collector.CollectAsync()).ToList();
        metrics[0].Timestamp.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public async Task CollectAsync_HasDriveCountTag()
    {
        var collector = CreateCollector();
        var metrics = (await collector.CollectAsync()).ToList();
        metrics[0].Tags.Should().ContainKey("drive_count");
    }

    // NormalizeDriveLetter is internal static — accessible via InternalsVisibleTo

    [Theory]
    [InlineData("C:", "C:")]
    [InlineData("C", "C:")]
    [InlineData("C:\\", "C:")]
    [InlineData("c", "C:")]
    [InlineData("c:", "C:")]
    [InlineData("D:\\", "D:")]
    public void NormalizeDriveLetter_VariousFormats(string input, string expected)
    {
        if (!OperatingSystem.IsWindows())
            return; // Normalization logic is Windows-specific

        DiskFreeCollector.NormalizeDriveLetter(input).Should().Be(expected);
    }
}
