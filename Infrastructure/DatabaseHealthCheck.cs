using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Systems_One_MQTT_Service.Collectors.Database;

namespace Systems_One_MQTT_Service.Infrastructure;

/// <summary>
/// Health check that reports SQL Server database connectivity.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public DatabaseHealthCheck(IOptions<DatabaseCollectorOptions> options)
    {
        var opts = options.Value;
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = opts.Server ?? string.Empty,
            InitialCatalog = opts.DatabaseName ?? string.Empty,
            UserID = opts.Username ?? string.Empty,
            Password = opts.Password ?? string.Empty,
            TrustServerCertificate = true,
            Encrypt = false,
            ConnectTimeout = Math.Min(opts.TimeoutSeconds, 5) // Health checks should be fast
        };
        _connectionString = builder.ConnectionString;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            return HealthCheckResult.Healthy("Database connected");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}
