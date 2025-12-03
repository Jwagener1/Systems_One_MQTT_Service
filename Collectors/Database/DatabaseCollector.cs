using System.Data;
using Microsoft.Data.SqlClient;
using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;
using Microsoft.Extensions.Options;

namespace Systems_One_MQTT_Service.Collectors.Database;

public class DatabaseCollector : IMetricCollector
{
    public string Name => "Database";

    private readonly DatabaseCollectorOptions _options;
    private readonly string _connectionString;

    public DatabaseCollector(IOptions<DatabaseCollectorOptions> options)
    {
        _options = options.Value;
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
    }

    public async Task<IEnumerable<Metric>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var metrics = new List<Metric>();

        var statusMetric = new Metric
        {
            Id = "db.connection.status",
            Name = "Database Connection Status",
            Source = "DB",
            Timestamp = DateTimeOffset.UtcNow,
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
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            statusMetric.Value = true;

            var nowUtc = DateTime.UtcNow;
            var endUtc = nowUtc.AddMinutes(-5);
            var startUtc = endUtc.AddMinutes(-5);

            var table = string.IsNullOrWhiteSpace(_options.TableName) ? "ItemLog" : _options.TableName!;

            var summary = await ItemLogQuery.ExecuteWindowSummaryAsync(conn, table, startUtc, endUtc, cancellationToken);
            metrics.Add(new Metric
            {
                Id = "db.itemlog.window.summary",
                Name = "ItemLog Summary (5-minute window)",
                Source = "DB",
                Timestamp = DateTimeOffset.UtcNow,
                Value = summary,
                Tags = new Dictionary<string, object>
                {
                    { "startUtc", startUtc },
                    { "endUtc", endUtc }
                }
            });

            var rows = await ItemLogQuery.ExecuteWindowAsync(conn, table, startUtc, endUtc, cancellationToken);
            metrics.Add(new Metric
            {
                Id = "db.itemlog.window.count",
                Name = "ItemLog Rows in 5-minute window",
                Source = "DB",
                Timestamp = DateTimeOffset.UtcNow,
                Value = rows.Count,
                Tags = new Dictionary<string, object>
                {
                    { "startUtc", startUtc },
                    { "endUtc", endUtc }
                }
            });

            if (rows.Count > 0)
            {
                metrics.Add(new Metric
                {
                    Id = "db.itemlog.window.sample",
                    Name = "ItemLog Sample Row",
                    Source = "DB",
                    Timestamp = DateTimeOffset.UtcNow,
                    Value = rows[0]
                });
            }
        }
        catch (Exception ex)
        {
            metrics.Add(new Metric
            {
                Id = "db.query.error",
                Name = "Database Query Error",
                Source = "DB",
                Timestamp = DateTimeOffset.UtcNow,
                Value = ex.Message
            });
        }
        finally
        {
            metrics.Add(statusMetric);
        }

        return metrics;
    }
}
