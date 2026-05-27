using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Systems_One_MQTT_Updater.Scheduling;

/// <summary>
/// Queries the last 7 days of production DB data to find the hour of day (0–23)
/// with the lowest average item throughput. Falls back to midnight (0) when the
/// DB is not configured, unreachable, or has fewer than 3 days of data.
/// </summary>
public class QuietWindowAnalyzer
{
    private readonly IConfiguration _config;
    private readonly ILogger<QuietWindowAnalyzer> _logger;

    // Cached result — refreshed once per day
    private int _preferredHour = 0;
    private double[]? _hourlyAverages; // index = hour 0-23
    private DateTime _lastAnalyzed = DateTime.MinValue;

    public int PreferredUpdateHour => _preferredHour;

    // Averages per hour — used by ActivityChecker to compute the quiet threshold
    public double[]? HourlyAverages => _hourlyAverages;

    public QuietWindowAnalyzer(IConfiguration config, ILogger<QuietWindowAnalyzer> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (_lastAnalyzed.Date == DateTime.Today)
        {
            _logger.LogTrace("Quiet window already analysed today — skipping");
            return;
        }

        var (connectionString, schemaType, tableName) = ReadDbConfig();

        if (connectionString is null)
        {
            _logger.LogInformation("DB not configured — quiet window defaults to midnight (00:00)");
            _preferredHour = 0;
            _lastAnalyzed = DateTime.Today;
            return;
        }

        try
        {
            var averages = await QueryHourlyAveragesAsync(connectionString, schemaType, tableName, cancellationToken);

            // Need at least 3 days of data for a reliable picture
            var coveredDays = await CountDaysWithDataAsync(connectionString, schemaType, tableName, cancellationToken);
            if (coveredDays < 3)
            {
                _logger.LogInformation(
                    "Only {Days} day(s) of DB data — insufficient for quiet window analysis, defaulting to midnight", coveredDays);
                _preferredHour = 0;
                _hourlyAverages = null;
                _lastAnalyzed = DateTime.Today;
                return;
            }

            _hourlyAverages = averages;

            // The quietest hour is the one with the lowest average item count
            var quietestHour = Array.IndexOf(averages, averages.Min());
            _preferredHour = quietestHour;
            _lastAnalyzed = DateTime.Today;

            _logger.LogInformation(
                "Quiet window analysis complete — preferred update hour: {Hour:00}:00 (avg {Avg:F1} items/hr)",
                _preferredHour, averages[_preferredHour]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Quiet window DB query failed — keeping previous preferred hour ({Hour})", _preferredHour);
        }
    }

    private async Task<double[]> QueryHourlyAveragesAsync(
        string connectionString, string schemaType, string tableName, CancellationToken cancellationToken)
    {
        var dateCol = GetDateColumn(schemaType);
        var safeTable = ValidateTableName(tableName);

        var sql = $@"
            SELECT HourOfDay, AVG(CAST(HourlyCount AS FLOAT)) AS AvgItems
            FROM (
                SELECT
                    CAST({dateCol} AS DATE)      AS Day,
                    DATEPART(HOUR, {dateCol})    AS HourOfDay,
                    COUNT(*)                     AS HourlyCount
                FROM [{safeTable}]
                WHERE {dateCol} >= DATEADD(DAY, -7, GETDATE())
                GROUP BY CAST({dateCol} AS DATE), DATEPART(HOUR, {dateCol})
            ) AS Grouped
            GROUP BY HourOfDay";

        var averages = new double[24];

        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 30;

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            int hour = reader.GetInt32(0);
            double avg = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);
            if (hour >= 0 && hour < 24)
                averages[hour] = avg;
        }

        return averages;
    }

    private async Task<int> CountDaysWithDataAsync(
        string connectionString, string schemaType, string tableName, CancellationToken cancellationToken)
    {
        var dateCol = GetDateColumn(schemaType);
        var safeTable = ValidateTableName(tableName);

        var sql = $@"
            SELECT COUNT(DISTINCT CAST({dateCol} AS DATE))
            FROM [{safeTable}]
            WHERE {dateCol} >= DATEADD(DAY, -7, GETDATE())";

        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 30;

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is DBNull || result is null ? 0 : Convert.ToInt32(result);
    }

    private (string? connectionString, string schemaType, string tableName) ReadDbConfig()
    {
        var server = _config["Database:Server"];
        if (string.IsNullOrWhiteSpace(server))
            return (null, "Default", "ItemLog");

        var schemaType  = _config["Database:SchemaType"] ?? "Default";
        var tableName   = SchemaToTableName(schemaType);

        var builder = new SqlConnectionStringBuilder
        {
            DataSource            = server,
            InitialCatalog        = _config["Database:DatabaseName"] ?? string.Empty,
            UserID                = _config["Database:Username"] ?? string.Empty,
            Password              = _config["Database:Password"] ?? string.Empty,
            TrustServerCertificate = true,
            Encrypt               = false,
            ConnectTimeout        = 30
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
