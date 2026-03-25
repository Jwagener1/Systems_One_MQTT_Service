using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Systems_One_MQTT_Service.Collectors.App;
using Systems_One_MQTT_Service.Tests.Fakes;

namespace Systems_One_MQTT_Service.Tests.Collectors.App;

public class AppCollectorTests
{
    private readonly FakeClock _clock = new();
    private readonly AppCollector _collector;

    public AppCollectorTests()
    {
        var options = Options.Create(new AppCollectorOptions
        {
            ExePath = "nonexistent_process_12345.exe",
            SettingsDir = "/tmp/nonexistent_settings_dir_12345"
        });
        _collector = new AppCollector(options, NullLogger<AppCollector>.Instance, _clock);
    }

    [Fact]
    public async Task CollectAsync_FirstCall_EmitsRunningMetric()
    {
        // First call should detect state change from null to false (process doesn't exist)
        var metrics = (await _collector.CollectAsync()).ToList();
        metrics.Should().Contain(m => m.Id == "app.running");
    }

    [Fact]
    public async Task CollectAsync_SecondCall_SameState_NoRunningMetric()
    {
        await _collector.CollectAsync(); // First call — emits
        var metrics = (await _collector.CollectAsync()).ToList(); // Second call — same state
        metrics.Should().NotContain(m => m.Id == "app.running");
    }

    [Fact]
    public async Task CollectAsync_SettingsDirMissing_DoesNotThrow()
    {
        var act = async () => await _collector.CollectAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Category_IsApp()
    {
        _collector.Category.Should().Be("App");
    }

    [Fact]
    public async Task CollectAsync_UsesInjectedClock()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        var running = metrics.FirstOrDefault(m => m.Id == "app.running");
        running.Should().NotBeNull();
        running!.Timestamp.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public async Task CollectAsync_RunningMetric_HasExpectedTags()
    {
        var metrics = (await _collector.CollectAsync()).ToList();
        var running = metrics.First(m => m.Id == "app.running");
        running.Tags.Should().ContainKey("process_name");
        running.Tags.Should().ContainKey("exe_path");
        running.Tags.Should().ContainKey("process_count");
        running.Tags.Should().ContainKey("path_match");
    }
}
