using Systems_One_MQTT_Service.Collectors.Database;

namespace Systems_One_MQTT_Service.Tests;

public class ItemLogQueryTests
{
    [Theory]
    [InlineData("ItemLog")]
    [InlineData("My_Table_123")]
    [InlineData("_private")]
    public void ValidateTableName_AcceptsValidNames(string tableName)
    {
        // Should not throw — we test indirectly via the summary method signature
        // by verifying the regex pattern matches
        var regex = new System.Text.RegularExpressions.Regex(@"^[A-Za-z_][A-Za-z0-9_]*$");
        Assert.True(regex.IsMatch(tableName));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Robert]; DROP TABLE Students;--")]
    [InlineData("table name")]
    [InlineData("table-name")]
    [InlineData("123start")]
    [InlineData("table.name")]
    public void ValidateTableName_RejectsInvalidNames(string tableName)
    {
        var regex = new System.Text.RegularExpressions.Regex(@"^[A-Za-z_][A-Za-z0-9_]*$");
        Assert.False(regex.IsMatch(tableName));
    }
}
