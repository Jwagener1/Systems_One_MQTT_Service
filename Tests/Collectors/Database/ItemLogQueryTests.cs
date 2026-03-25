using FluentAssertions;
using Systems_One_MQTT_Service.Collectors.Database;

namespace Systems_One_MQTT_Service.Tests.Collectors.Database;

public class ItemLogQueryTests
{
    [Theory]
    [InlineData("ItemLog")]
    [InlineData("My_Table_123")]
    [InlineData("_private")]
    public void ValidateTableName_ValidNames_ReturnsName(string tableName)
    {
        var result = ItemLogQuery.ValidateTableName(tableName);
        result.Should().Be(tableName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Robert]; DROP TABLE Students;--")]
    [InlineData("table name")]
    [InlineData("table-name")]
    [InlineData("123start")]
    [InlineData("table.name")]
    public void ValidateTableName_InvalidNames_ThrowsArgumentException(string tableName)
    {
        var act = () => ItemLogQuery.ValidateTableName(tableName);
        act.Should().Throw<ArgumentException>();
    }
}
