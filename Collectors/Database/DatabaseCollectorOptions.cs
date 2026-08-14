namespace Systems_One_MQTT_Service.Collectors.Database;

public enum DbSchemaType
{
    Default,
    Snowsoft,
    Madibana,
    Twinsaver,
    FreightSnap
}

public class DatabaseCollectorOptions
{
    public string? Server { get; set; }
    public string? DatabaseName { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public DbSchemaType SchemaType { get; set; } = DbSchemaType.Default;

    /// <summary>
    /// Returns the SQL table name implied by <see cref="SchemaType"/>.
    /// Default → ItemLog, Madibana → tbl_Measurement, Snowsoft → tbl_Scanned_Items.
    /// </summary>
    public string GetTableName() => SchemaType switch
    {
        DbSchemaType.Snowsoft    => "tbl_Scanned_Items",
        DbSchemaType.Madibana    => "tbl_Measurement",
        DbSchemaType.Twinsaver   => "tbl_Line_Data",
        DbSchemaType.FreightSnap => "FreightScans",
        _                        => "ItemLog"
    };
}