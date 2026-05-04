namespace Systems_One_MQTT_Service.Publishing.Mqtt;

public class MqttSettings
{
    public string? BrokerUrl { get; set; }
    public int BrokerPort { get; set; } = 1883;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string BaseTopic { get; set; } = "systems-one";
    public bool ValidateCertificate { get; set; } = true;
    public bool EncryptionTLS { get; set; } = false;
    public string? BasePath { get; set; }
    
    // Hierarchical topic structure
    public string? Company { get; set; }
    public string? Location { get; set; }
    public string? MachineId { get; set; }
    public string? SerialNumber { get; set; }

    public string ClientId
    {
        get
        {
            var parts = new[] { Company, Location, MachineId, SerialNumber }
                .Where(s => !string.IsNullOrWhiteSpace(s));
            var id = string.Join('_', parts);
            // Always fall back to MachineName so ClientId is never empty.
            // An empty or duplicate ClientId causes the broker to kick existing connections.
            return string.IsNullOrWhiteSpace(id) ? Environment.MachineName : id;
        }
    }
}