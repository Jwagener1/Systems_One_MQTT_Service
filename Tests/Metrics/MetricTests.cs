using FluentAssertions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Tests.Metrics;

public class MetricTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var metric = new Metric();
        metric.Id.Should().Be(string.Empty);
        metric.Name.Should().Be(string.Empty);
        metric.Source.Should().Be(string.Empty);
        metric.Tags.Should().BeNull();
        metric.Unit.Should().BeNull();
    }

    [Fact]
    public void Value_AcceptsString()
    {
        var metric = new Metric { Value = "test" };
        metric.Value.Should().Be("test");
    }

    [Fact]
    public void Value_AcceptsNumber()
    {
        var metric = new Metric { Value = 42.5 };
        Convert.ToDouble(metric.Value).Should().Be(42.5);
    }

    [Fact]
    public void Value_AcceptsDictionary()
    {
        var dict = new Dictionary<string, int> { { "a", 1 } };
        var metric = new Metric { Value = dict };
        metric.Value.Should().Be(dict);
    }

    [Fact]
    public void Value_AcceptsBool()
    {
        var metric = new Metric { Value = true };
        metric.Value.Should().Be(true);
    }

    [Fact]
    public void Tags_CanBeSet()
    {
        var metric = new Metric
        {
            Tags = new Dictionary<string, object> { { "key", "value" } }
        };
        metric.Tags.Should().ContainKey("key");
    }
}
