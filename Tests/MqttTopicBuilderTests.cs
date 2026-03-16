using Systems_One_MQTT_Service.Publishing.Mqtt;

namespace Systems_One_MQTT_Service.Tests;

public class MqttTopicBuilderTests
{
    [Fact]
    public void Build_CreatesExpectedTopic()
    {
        var result = MqttTopicBuilder.Build("test", "MACHINE1", "OS", "os.version");
        Assert.Equal("test/MACHINE1/OS/version", result);
    }

    [Fact]
    public void Build_DoesNotDoubleScopePrefix()
    {
        var result = MqttTopicBuilder.Build("base", "HOST", "DB", "db.connection");
        Assert.Equal("base/HOST/DB/connection", result);
    }

    [Fact]
    public void Build_PreservesPathWhenNoScopePrefix()
    {
        var result = MqttTopicBuilder.Build("base", "HOST", "OS", "cpu.usage");
        Assert.Equal("base/HOST/OS/cpu/usage", result);
    }
}
