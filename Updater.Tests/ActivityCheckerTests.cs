using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Systems_One_MQTT_Updater.Scheduling;

namespace Systems_One_MQTT_Updater.Tests;

public class ActivityCheckerTests
{
    private static ActivityChecker BuildChecker(double thresholdPercent = 10.0)
    {
        var config = new ConfigurationBuilder().Build(); // no DB:Server → fail-open
        var settings = Options.Create(new UpdaterSettings { QuietThresholdPercent = thresholdPercent });
        return new ActivityChecker(config, settings, NullLogger<ActivityChecker>.Instance);
    }

    [Fact]
    public async Task IsQuiet_NoDbConfigured_ReturnsTrue()
    {
        var checker = BuildChecker();
        var result = await checker.IsQuietAsync(null, CancellationToken.None);
        Assert.True(result);
    }

    [Fact]
    public async Task IsQuiet_NullHourlyAverages_ReturnsTrue()
    {
        var checker = BuildChecker();
        var result = await checker.IsQuietAsync(hourlyAverages: null, CancellationToken.None);
        Assert.True(result);
    }

    [Fact]
    public async Task IsQuiet_ZeroAverageForCurrentHour_ReturnsTrue()
    {
        var checker = BuildChecker();
        var averages = new double[24]; // all zeros
        var result = await checker.IsQuietAsync(averages, CancellationToken.None);
        Assert.True(result);
    }
}
