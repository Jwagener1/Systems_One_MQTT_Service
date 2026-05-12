using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Systems_One_MQTT_Service.Collectors.Database;
using Systems_One_MQTT_Service.Tests.Fakes;

namespace Systems_One_MQTT_Service.Tests.Collectors.Database;

public class DatabaseCollectorOptionsTests
{
    [Theory]
    [InlineData(DbSchemaType.Default,  "ItemLog")]
    [InlineData(DbSchemaType.Snowsoft, "tbl_Scanned_Items")]
    [InlineData(DbSchemaType.Madibana, "tbl_Measurement")]
    public void GetTableName_ReturnsExpectedTablePerSchema(DbSchemaType schema, string expectedTable)
    {
        var options = new DatabaseCollectorOptions { SchemaType = schema };

        options.GetTableName().Should().Be(expectedTable);
    }

    [Fact]
    public void DefaultSchemaType_IsDefault()
    {
        var options = new DatabaseCollectorOptions();

        options.SchemaType.Should().Be(DbSchemaType.Default);
        options.GetTableName().Should().Be("ItemLog");
    }

    [Fact]
    public void DefaultTimeoutSeconds_Is30()
    {
        new DatabaseCollectorOptions().TimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public void Options_DoesNotExposeTableNameProperty()
    {
        // The schema-driven design removed the user-configurable TableName property —
        // guard against accidental reintroduction.
        typeof(DatabaseCollectorOptions)
            .GetProperty("TableName")
            .Should().BeNull("TableName was removed in favour of GetTableName() driven by SchemaType");
    }
}

public class DatabaseCollectorTests
{
    private readonly FakeClock _clock = new();

    [Fact]
    public async Task CollectAsync_NoServerConfigured_ReturnsEmpty()
    {
        var options = Options.Create(new DatabaseCollectorOptions { Server = null });
        var collector = new DatabaseCollector(options, NullLogger<DatabaseCollector>.Instance, _clock);

        var metrics = await collector.CollectAsync();

        metrics.Should().BeEmpty();
    }

    [Fact]
    public async Task CollectAsync_BlankServer_ReturnsEmpty()
    {
        var options = Options.Create(new DatabaseCollectorOptions { Server = "   " });
        var collector = new DatabaseCollector(options, NullLogger<DatabaseCollector>.Instance, _clock);

        var metrics = await collector.CollectAsync();

        metrics.Should().BeEmpty();
    }

    [Fact]
    public async Task CollectAsync_UnreachableServer_EmitsConnectionFalseAndError()
    {
        var options = Options.Create(new DatabaseCollectorOptions
        {
            Server         = "127.0.0.1,1",        // unreachable
            DatabaseName   = "Systems_One",
            Username       = "sa",
            Password       = "x",
            TimeoutSeconds = 1,
            SchemaType     = DbSchemaType.Madibana
        });
        var collector = new DatabaseCollector(options, NullLogger<DatabaseCollector>.Instance, _clock);

        var metrics = (await collector.CollectAsync()).ToList();

        var status = metrics.Should().ContainSingle(m => m.Id == "db.connection").Subject;
        status.Value.Should().Be(false);
        status.Tags.Should().ContainKey("schema").WhoseValue.Should().Be(nameof(DbSchemaType.Madibana));
        status.Tags.Should().ContainKey("table").WhoseValue.Should().Be("tbl_Measurement");

        metrics.Should().Contain(m => m.Id == "db.query.error");
    }

    [Theory]
    [InlineData(DbSchemaType.Default,  "ItemLog")]
    [InlineData(DbSchemaType.Snowsoft, "tbl_Scanned_Items")]
    [InlineData(DbSchemaType.Madibana, "tbl_Measurement")]
    public async Task CollectAsync_StatusMetricTagsExposeResolvedTable(DbSchemaType schema, string expectedTable)
    {
        var options = Options.Create(new DatabaseCollectorOptions
        {
            Server         = "127.0.0.1,1",
            DatabaseName   = "x",
            Username       = "x",
            Password       = "x",
            TimeoutSeconds = 1,
            SchemaType     = schema
        });
        var collector = new DatabaseCollector(options, NullLogger<DatabaseCollector>.Instance, _clock);

        var metrics = (await collector.CollectAsync()).ToList();

        var status = metrics.Single(m => m.Id == "db.connection");
        status.Tags["table"].Should().Be(expectedTable);
        status.Tags["schema"].Should().Be(schema.ToString());
    }
}
