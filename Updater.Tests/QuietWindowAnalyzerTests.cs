using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Systems_One_MQTT_Updater.Scheduling;

namespace Systems_One_MQTT_Updater.Tests;

public class QuietWindowAnalyzerTests
{
    [Fact]
    public async Task RefreshAsync_NoDbConfigured_DefaultsMidnight()
    {
        var config = new ConfigurationBuilder().Build(); // empty config → no DB:Server
        var analyzer = new QuietWindowAnalyzer(config, NullLogger<QuietWindowAnalyzer>.Instance);

        await analyzer.RefreshAsync(CancellationToken.None);

        Assert.Equal(0, analyzer.PreferredUpdateHour);
    }

    [Fact]
    public void PreferredHour_DefaultsToMidnight_BeforeFirstRefresh()
    {
        var config = new ConfigurationBuilder().Build();
        var analyzer = new QuietWindowAnalyzer(config, NullLogger<QuietWindowAnalyzer>.Instance);

        // No refresh called yet — should still default safely
        Assert.Equal(0, analyzer.PreferredUpdateHour);
    }
}
