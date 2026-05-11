namespace Systems_One_MQTT_Service.Collectors.Database;

public enum DbSchemaType
{
    Default,
    Snowsoft,
    Madibana
}

public class DatabaseCollectorOptions
{
    public string? Server { get; set; }
    public string? DatabaseName { get; set; }
    public string? TableName { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public DbSchemaType SchemaType { get; set; } = DbSchemaType.Default;
}