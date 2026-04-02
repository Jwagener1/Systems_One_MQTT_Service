using FluentAssertions;
using Systems_One_MQTT_Service.Publishing.Mqtt;

namespace Systems_One_MQTT_Service.Tests.Publishing;

public class MqttTopicBuilderTests
{
    // Legacy structure tests (backward compatibility)
    [Fact]
    public void Build_LegacyStructure_OsMetric_StripsScopeAndFlattens()
    {
        var result = MqttTopicBuilder.Build("systems-one", "IMOGEN", "OS", "os.version");
        result.Should().Be("systems-one/IMOGEN/OS/version");
    }

    [Fact]
    public void Build_LegacyStructure_CpuUsage_FlattensToFirstSegment()
    {
        var result = MqttTopicBuilder.Build("systems-one", "IMOGEN", "OS", "cpu.usage");
        result.Should().Be("systems-one/IMOGEN/OS/cpu");
    }

    [Fact]
    public void Build_LegacyStructure_Memory_FlattensToFirstSegment()
    {
        var result = MqttTopicBuilder.Build("systems-one", "IMOGEN", "OS", "memory");
        result.Should().Be("systems-one/IMOGEN/OS/memory");
    }

    [Fact]
    public void Build_LegacyStructure_DbScope_StripsScopePrefix()
    {
        var result = MqttTopicBuilder.Build("base", "HOST", "DB", "db.connection");
        result.Should().Be("base/HOST/DB/connection");
    }

    [Fact]
    public void Build_LegacyStructure_MachineName_WithHyphens()
    {
        var result = MqttTopicBuilder.Build("base", "MY-PC-01", "OS", "os.version");
        result.Should().Be("base/MY-PC-01/OS/version");
    }

    [Fact]
    public void Build_LegacyStructure_AppRunning_StripsScopePrefix()
    {
        var result = MqttTopicBuilder.Build("systems-one", "IMOGEN", "App", "app.running");
        result.Should().Be("systems-one/IMOGEN/App/running");
    }

    [Fact]
    public void Build_LegacyStructure_SingleSegmentId_NoStripping()
    {
        var result = MqttTopicBuilder.Build("base", "HOST", "OS", "temperature");
        result.Should().Be("base/HOST/OS/temperature");
    }

    // Hierarchical structure tests
    [Fact]
    public void Build_HierarchicalStructure_OsMetric_IncludesCompanyLocationMachine()
    {
        var result = MqttTopicBuilder.Build("systems-one", "PEPKOR", "WRH", "DIM2", "OS", "os.version");
        result.Should().Be("systems-one/PEPKOR/WRH/DIM2/OS/version");
    }

    [Fact]
    public void Build_HierarchicalStructure_CpuUsage_FlattensToFirstSegment()
    {
        var result = MqttTopicBuilder.Build("systems-one", "PEPKOR", "WRH", "DIM2", "OS", "cpu.usage");
        result.Should().Be("systems-one/PEPKOR/WRH/DIM2/OS/cpu");
    }

    [Fact]
    public void Build_HierarchicalStructure_DbConnection_StripsScopePrefix()
    {
        var result = MqttTopicBuilder.Build("systems-one", "PEPKOR", "WRH", "DIM2", "DB", "db.connection");
        result.Should().Be("systems-one/PEPKOR/WRH/DIM2/DB/connection");
    }

    [Fact]
    public void Build_HierarchicalStructure_AppRunning_StripsScopePrefix()
    {
        var result = MqttTopicBuilder.Build("systems-one", "PEPKOR", "WRH", "DIM2", "App", "app.running");
        result.Should().Be("systems-one/PEPKOR/WRH/DIM2/App/running");
    }

    [Fact]
    public void BuildStatusTopic_HierarchicalStructure_CorrectFormat()
    {
        var result = MqttTopicBuilder.BuildStatusTopic("systems-one", "PEPKOR", "WRH", "DIM2");
        result.Should().Be("systems-one/PEPKOR/WRH/DIM2/status");
    }
}
