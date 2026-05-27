using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Systems_One_MQTT_Updater.Scheduling;

/// <summary>
/// Checks live DB activity over the last 5 minutes.
/// Returns true when the line is quiet enough to apply an update safely.
/// </summary>
public class ActivityChecker
{
    private readonly IConfiguration _config;
    private readonly UpdaterSettings _settings;
    private readonly ILogger<ActivityChecker> _logger;

    public ActivityChecker(IConfiguration config, IOptions<UpdaterSettings> options, ILogger<ActivityChecker> logger)
    {
        _config = config;
        _settings = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Returns true when the item count in the last 5 minutes is at or below
    /// QuietThresholdPercent of the historical average for the current hour.
    /// Always returns true when the DB is not configured (fail-open).
    /// </summary>
    public async Task<bool> IsQuietAsync(double[]? hourlyAverages, CancellationToken cancellationToken)
    {
        var (connectionString, schemaType, tableName) = ReadDbConfig();

        if (connectionString is null)
        {
            _logger.LogDebug("DB not configured — treating line as quiet");
            return true;
        }

        int currentHour = DateTime.Now.Hour;
        double historicalAvg = hourlyAverages?[currentHour] ?? 0;

        // If no historical data for this hour, treat it as quiet
        if (historicalAvg <= 0)
        {
            _logger.LogDebug("No historical average for hour {Hour} — treating as quiet", currentHour);
            return true;
        }

        double threshold = historicalAvg * (_settings.QuietThresholdPercent / 100.0);

        try
        {
            int recentCount = await CountRecentItemsAsync(connectionString, schemaType, tableName, cancellationToken);

            bool isQuiet = recentCount <= threshold;
            _logger.LogDebug(
                "Activity check: last-5-min items={Count}, threshold={Threshold:F1} ({Percent}% of {Avg:F1}/hr avg) → {Result}",
                recentCount, threshold, _settings.QuietThresholdPercent, historicalAvg,
                isQuiet ? "QUIET" : "BUSY");

            return isQuiet;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Activity check DB query failed — treating line as quiet to avoid indefinite deferral");
            return true;
        }
    }

    private async Task<int> CountRecentItemsAsync(
        string connectionString, string schemaType, string tableName, CancellationToken cancellationToken)
    {
        var dateCol   = GetDateColumn(schemaType);
        var safeTable = ValidateTableName(tableName);

        var sql = $@"
            SELECT COUNT(*)
            FROM [{safeTable}]
            WHERE {dateCol} >= DATEADD(MINUTE, -5, GETDATE())";

        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 15;

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is DBNull || result is null ? 0 : Convert.ToInt32(result);
    }

    private (string? connectionString, string schemaType, string tableName) ReadDbConfig()
    {
        var server = _config["Database:Server"];
        if (string.IsNullOrWhiteSpace(server))
            return (null, "Default", "ItemLog");

        var schemaType = _config["Database:SchemaType"] ?? "Default";
        var tableName  = SchemaToTableName(schemaType);

        var builder = new SqlConnectionStringBuilder
        {
            DataSource             = server,
            InitialCatalog         = _config["Database:DatabaseName"] ?? string.Empty,
            UserID                 = _config["Database:Username"] ?? string.Empty,
            Password               = _config["Database:Password"] ?? string.Empty,
            TrustServerCertificate = true,
            Encrypt                = false,
            ConnectTimeout         = 15
        };

        return (builder.ConnectionString, schemaType, tableName);
    }

    private static string GetDateColumn(string schemaType) => schemaType.ToLowerInvariant() switch
    {
        "snowsoft"  => "Item_Date_Time",
        "madibana"  => "Item_Date_Time",
        "twinsaver" => "Print_Date",
        _           => "ItemDateTime"
    };

    private static string SchemaToTableName(string schemaType) => schemaType.ToLowerInvariant() switch
    {
        "snowsoft"  => "tbl_Scanned_Items",
        "madibana"  => "tbl_Measurement",
        "twinsaver" => "tbl_Line_Data",
        _           => "ItemLog"
    };

    private static readonly Regex SafeTableNameRegex =
        new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private static string ValidateTableName(string name)
    {
        if (!SafeTableNameRegex.IsMatch(name))
            throw new ArgumentException($"Unsafe table name: '{name}'");
        return name;
    }
}
