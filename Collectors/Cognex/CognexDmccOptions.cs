namespace Systems_One_MQTT_Service.Collectors.Cognex;

public class CognexDmccOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 23;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
    public int ReportHourUtc { get; set; } = 6;
}
