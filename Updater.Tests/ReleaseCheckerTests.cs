using Xunit;
using Systems_One_MQTT_Updater.GitHub;

namespace Systems_One_MQTT_Updater.Tests;

public class ReleaseCheckerTests
{
    [Theory]
    [InlineData("2026.05.27.10", "2026.05.27.9",  true)]   // same day, higher build
    [InlineData("2026.05.28.1",  "2026.05.27.99", true)]   // next day
    [InlineData("2026.06.01.1",  "2026.05.31.50", true)]   // next month
    [InlineData("2027.01.01.1",  "2026.12.31.99", true)]   // next year
    [InlineData("2026.05.27.9",  "2026.05.27.9",  false)]  // identical
    [InlineData("2026.05.27.8",  "2026.05.27.9",  false)]  // older build
    [InlineData("2026.05.26.99", "2026.05.27.1",  false)]  // older day
    public void IsNewer_ReturnsExpected(string candidate, string installed, bool expected)
    {
        Assert.Equal(expected, ReleaseChecker.IsNewer(candidate, installed));
    }

    [Theory]
    [InlineData("0.0.0.0", "1.2.3.4", false)] // installed newer than zero-default
    [InlineData("1.2.3.4", "0.0.0.0", true)]  // manifest newer than zero-default
    public void IsNewer_ZeroVersionEdgeCases(string candidate, string installed, bool expected)
    {
        Assert.Equal(expected, ReleaseChecker.IsNewer(candidate, installed));
    }

    [Theory]
    [InlineData("not-a-version", "2026.05.27.1", false)] // malformed candidate → never update
    [InlineData("2026.05.27.1", "not-a-version", true)]  // malformed installed → treat as 0.0.0.0
    public void IsNewer_MalformedInput_DoesNotThrow(string candidate, string installed, bool expected)
    {
        Assert.Equal(expected, ReleaseChecker.IsNewer(candidate, installed));
    }
}
