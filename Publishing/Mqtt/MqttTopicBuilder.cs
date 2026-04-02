namespace Systems_One_MQTT_Service.Publishing.Mqtt;

public static class MqttTopicBuilder
{
    /// <summary>
    /// Builds an MQTT topic from metric metadata with hierarchical structure.
    /// 
    /// Structure: {baseTopic}/{company}/{location}/{machine}/{scope}/{metric}
    /// 
    /// The metric ID is split on dots. The scope prefix is stripped if present.
    /// Only the first remaining segment is used — no subtopics.
    /// 
    /// Examples:
    ///   ("systems-one", "PEPKOR", "WRH", "DIM2", "OS", "cpu.usage")       → "systems-one/PEPKOR/WRH/DIM2/OS/cpu"
    ///   ("systems-one", "PEPKOR", "WRH", "DIM2", "OS", "memory.total")    → "systems-one/PEPKOR/WRH/DIM2/OS/memory"
    ///   ("systems-one", "PEPKOR", "WRH", "DIM2", "OS", "os.version")      → "systems-one/PEPKOR/WRH/DIM2/OS/version"
    ///   ("systems-one", "PEPKOR", "WRH", "DIM2", "DB", "db.connection")   → "systems-one/PEPKOR/WRH/DIM2/DB/connection"
    /// </summary>
    public static string Build(string baseTopic, string company, string location, string machine, string scope, string metricId)
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

        return string.Join('/', baseTopic, company, location, machine, scope, topicName);
    }

    /// <summary>
    /// Builds a status topic for machine online/offline status.
    /// Structure: {baseTopic}/{company}/{location}/{machine}/status
    /// </summary>
    public static string BuildStatusTopic(string baseTopic, string company, string location, string machine)
    {
        return string.Join('/', baseTopic, company, location, machine, "status");
    }

    /// <summary>
    /// Legacy method for backward compatibility when hierarchical structure is not configured.
    /// Falls back to: {baseTopic}/{machine}/{scope}/{metric}
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
