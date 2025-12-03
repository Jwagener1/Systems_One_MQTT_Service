namespace Systems_One_MQTT_Service.Publishing.Mqtt;

public static class MqttTopicBuilder
{
    public static string Build(string baseTopic, string machine, string scope, string metricId)
    {
        var path = metricId.Replace('.', '/');
        var scopeLower = scope.ToLowerInvariant();
        if (path.StartsWith(scopeLower + "/"))
        {
            path = path.Substring(scopeLower.Length + 1);
        }
        return string.Join('/', baseTopic, machine, scope, path);
    }
}