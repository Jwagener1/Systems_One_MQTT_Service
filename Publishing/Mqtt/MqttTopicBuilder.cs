namespace Systems_One_MQTT_Service.Publishing.Mqtt;

public static class MqttTopicBuilder
{
    /// <summary>
    /// Builds an MQTT topic from metric metadata.
    /// 
    /// The metric ID is split on dots. The scope prefix is stripped if present.
    /// Only the first remaining segment is used — no subtopics.
    /// 
    /// Examples:
    ///   ("systems-one", "IMOGEN", "OS", "cpu.usage")       → "systems-one/IMOGEN/OS/cpu"
    ///   ("systems-one", "IMOGEN", "OS", "memory.total")    → "systems-one/IMOGEN/OS/memory"
    ///   ("systems-one", "IMOGEN", "OS", "os.version")      → "systems-one/IMOGEN/OS/version"
    ///   ("systems-one", "IMOGEN", "OS", "os.drives")       → "systems-one/IMOGEN/OS/drives"
    ///   ("systems-one", "IMOGEN", "DB", "db.connection")   → "systems-one/IMOGEN/DB/connection"
    ///   ("systems-one", "IMOGEN", "App", "app.running")    → "systems-one/IMOGEN/App/running"
    /// </summary>
    public static string Build(string baseTopic, string machine, string scope, string metricId)
    {
        // Split metric ID on dots
        var parts = metricId.Split('.');
        var scopeLower = scope.ToLowerInvariant();

        // Strip scope prefix if present (e.g., "os.version" with scope "OS" → skip "os")
        var startIndex = 0;
        if (parts.Length > 1 && string.Equals(parts[0], scopeLower, StringComparison.OrdinalIgnoreCase))
        {
            startIndex = 1;
        }

        // Take only the first meaningful segment — no subtopics
        var topicName = startIndex < parts.Length ? parts[startIndex] : metricId;

        return string.Join('/', baseTopic, machine, scope, topicName);
    }
}
