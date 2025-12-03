namespace Systems_One_MQTT_Service.Collectors.Database;

public class DatabaseCollectorOptions
{
    public string? Server { get; set; }
    public string? DatabaseName { get; set; }
    public string? TableName { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int TimeoutSeconds { get; set; } = 5;
}