namespace Systems_One_MQTT_Service.Collectors.PLC;

public class PlcErrorCollectorOptions
{
    public string? PlcMqttBrokerUrl { get; set; }
    public int PlcMqttPort { get; set; } = 1883;
    public string? PlcMqttUsername { get; set; }
    public string? PlcMqttPassword { get; set; }
    public List<string> ErrorTopics { get; set; } = new()
    {
        "+/plc/errors/#",
        "+/pe/alignment/#"
    };
    public bool ForwardCriticalErrors { get; set; } = true;
}
