using FluentAssertions;
using Systems_One_MQTT_Service.Publishing.Mqtt;

namespace Systems_One_MQTT_Service.Tests.Publishing;

public class MqttTopicBuilderTests
{
    [Fact]
    public void Build_StandardMetric_StripsScopePrefix()
    {
        var result = MqttTopicBuilder.Build("base", "HOST", "OS", "os.version");
        result.Should().Be("base/HOST/OS/version");
    }

    [Fact]
    public void Build_NoScopePrefix_PreservesFullPath()
    {
        var result = MqttTopicBuilder.Build("base", "HOST", "OS", "cpu.usage");
        result.Should().Be("base/HOST/OS/cpu/usage");
    }

    [Fact]
    public void Build_DbScope_StripsScopePrefix()
    {
        var result = MqttTopicBuilder.Build("base", "HOST", "DB", "db.connection");
        result.Should().Be("base/HOST/DB/connection");
    }

    [Fact]
    public void Build_MachineName_WithHyphens()
    {
        var result = MqttTopicBuilder.Build("base", "MY-PC-01", "OS", "os.version");
        result.Should().Be("base/MY-PC-01/OS/version");
    }

    [Fact]
    public void Build_MetricIdDotsToSlashes()
    {
        var result = MqttTopicBuilder.Build("b", "H", "OS", "a.b.c");
        result.Should().Be("b/H/OS/a/b/c");
    }

    [Fact]
    public void Build_EmptyMetricId_DoesNotThrow()
    {
        var act = () => MqttTopicBuilder.Build("base", "HOST", "OS", "");
        act.Should().NotThrow();
    }
}
