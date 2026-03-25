namespace Systems_One_MQTT_Service.Collectors.OS;

public class DiskFreeCollectorOptions
{
    public List<DiskMonitorEntry> Monitors { get; set; } = new();
}

public class DiskMonitorEntry
{
    public string? Path { get; set; }
}
