namespace Systems_One_MQTT_Service.Publishing.Mqtt;

public class MqttSettings
{
    public string? BrokerUrl { get; set; }
    public int BrokerPort { get; set; } = 1883;
    public string? ClientId { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string BaseTopic { get; set; } = "systems-one";
    public bool ValidateCertificate { get; set; } = true;
    public bool EncryptionTLS { get; set; } = false;
    public string? BasePath { get; set; }
}