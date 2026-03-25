using System.Data;
using Microsoft.Data.SqlClient;
using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;
using Microsoft.Extensions.Options;

namespace Systems_One_MQTT_Service.Collectors.Database;

public class DatabaseCollector : IMetricCollector
{
    public string Name => "Database";
    public string Category => "DB";

    private readonly DatabaseCollectorOptions _options;
    private readonly string _connectionString;
    private readonly ILogger<DatabaseCollector> _logger;
    private readonly IClock _clock;

    public DatabaseCollector(IOptions<DatabaseCollectorOptions> options, ILogger<DatabaseCollector> logger, IClock clock)
    {
        _options = options.Value;
        _logger = logger;
        _clock = clock;
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = _options.Server ?? string.Empty,
            InitialCatalog = _options.DatabaseName ?? string.Empty,
            UserID = _options.Username ?? string.Empty,
            Password = _options.Password ?? string.Empty,
            TrustServerCertificate = true,
            Encrypt = false,
            ConnectTimeout = _options.TimeoutSeconds
        };
        _connectionString = builder.ConnectionString;
        _logger.LogDebug("DatabaseCollector initialized: Server={Server}, Database={Database}, Table={Table}, Timeout={Timeout}s",
            _options.Server, _options.DatabaseName, _options.TableName, _options.TimeoutSeconds);
    }

    public async Task<IEnumerable<Metric>> CollectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("DatabaseCollector.CollectAsync started");
        var metrics = new List<Metric>();

        var statusMetric = new Metric
        {
            Id = "db.connection",
            Name = "Database Connection Status",
            Source = "DB",
            Timestamp = _clock.UtcNow,
            Value = false,
            Tags = new Dictionary<string, object>
            {
                { "server", _options.Server ?? string.Empty },
                { "database", _options.DatabaseName ?? string.Empty },
                { "table", _options.TableName ?? string.Empty }
            }
        };

        try
        {
            _logger.LogDebug("Opening SQL connection to {Server}/{Database}", _options.Server, _options.DatabaseName);
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            statusMetric.Value = true;
            _logger.LogDebug("SQL connection established");

            var nowUtc = DateTime.UtcNow;
            var endUtc = nowUtc.AddMinutes(-5);
            var startUtc = endUtc.AddMinutes(-5);
            var table = string.IsNullOrWhiteSpace(_options.TableName) ? "ItemLog" : _options.TableName!;
            _logger.LogTrace("Querying {Table} for window {StartUtc} to {EndUtc}", table, startUtc, endUtc);

            var summary = await ItemLogQuery.ExecuteWindowSummaryAsync(conn, table, startUtc, endUtc, cancellationToken);
            
            _logger.LogDebug("Query returned summary: TotalItems={Total}, NoRead={NoRead}, DataSent={DataSent}",
                summary.GetValueOrDefault("Total_Items"), summary.GetValueOrDefault("No_Read"), summary.GetValueOrDefault("Data_Sent"));
            
            metrics.Add(new Metric
            {
                Id = "db.itemlog.summary",
                Name = "ItemLog Summary (5-minute window)",
                Source = "DB",
                Timestamp = _clock.UtcNow,
                Value = summary,
                Tags = new Dictionary<string, object>
                {
                    { "startUtc", startUtc },
                    { "endUtc", endUtc }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database query failed: {Server}/{Database}", _options.Server, _options.DatabaseName);
            metrics.Add(new Metric
            {
                Id = "db.query.error",
                Name = "Database Query Error",
                Source = "DB",
                Timestamp = _clock.UtcNow,
                Value = ex.Message
            });
        }
        finally
        {
            metrics.Add(statusMetric);
        }

        _logger.LogTrace("DatabaseCollector.CollectAsync finished: {MetricCount} metrics", metrics.Count);
        return metrics;
    }
}
