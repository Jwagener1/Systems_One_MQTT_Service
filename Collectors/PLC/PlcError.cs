namespace Systems_One_MQTT_Service.Collectors.PLC;

public class PlcError
{
    public DateTimeOffset Timestamp { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string? MachineId { get; set; }
}
