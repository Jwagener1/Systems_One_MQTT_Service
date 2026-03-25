using FluentAssertions;
using Systems_One_MQTT_Service.Publishing.Mqtt;

namespace Systems_One_MQTT_Service.Tests.Publishing;

public class MqttTopicBuilderTests
{
    [Fact]
    public void Build_OsMetric_StripsScopeAndFlattens()
    {
        var result = MqttTopicBuilder.Build("systems-one", "IMOGEN", "OS", "os.version");
        result.Should().Be("systems-one/IMOGEN/OS/version");
    }

    [Fact]
    public void Build_CpuUsage_FlattensToFirstSegment()
    {
        var result = MqttTopicBuilder.Build("systems-one", "IMOGEN", "OS", "cpu.usage");
        result.Should().Be("systems-one/IMOGEN/OS/cpu");
    }

    [Fact]
    public void Build_Memory_FlattensToFirstSegment()
    {
        var result = MqttTopicBuilder.Build("systems-one", "IMOGEN", "OS", "memory");
        result.Should().Be("systems-one/IMOGEN/OS/memory");
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
    public void Build_AppRunning_StripsScopePrefix()
    {
        var result = MqttTopicBuilder.Build("systems-one", "IMOGEN", "App", "app.running");
        result.Should().Be("systems-one/IMOGEN/App/running");
    }

    [Fact]
    public void Build_SingleSegmentId_NoStripping()
    {
        var result = MqttTopicBuilder.Build("base", "HOST", "OS", "temperature");
        result.Should().Be("base/HOST/OS/temperature");
    }
}
