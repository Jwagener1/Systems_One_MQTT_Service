namespace Systems_One_MQTT_Service.Publishing.Mqtt;

public static class MqttTopicBuilder
{
    public static string Build(string baseTopic, string machine, string scope, string metricId)
        => string.Join('/', baseTopic, machine, scope, metricId.Replace('.', '/'));
}