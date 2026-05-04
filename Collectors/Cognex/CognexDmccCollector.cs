using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.Cognex;

/// <summary>
/// Collects daily performance reports from a Cognex reader via DMCC (telnet) protocol.
/// Only runs once per day at the configured hour.
/// </summary>
public class CognexDmccCollector : IMetricCollector
{
    public string Name => "Cognex DMCC";
    public string Category => "Cognex";

    private readonly CognexDmccOptions _options;
    private readonly ILogger<CognexDmccCollector> _logger;
    private readonly IClock _clock;
    private DateTime? _lastReportDate;

    public CognexDmccCollector(IOptions<CognexDmccOptions> options, ILogger<CognexDmccCollector> logger, IClock clock)
    {
        _options = options.Value;
        _logger = logger;
        _clock = clock;
    }

    public async Task<IEnumerable<Metric>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var metrics = new List<Metric>();
        var now = _clock.Now;

        // Skip if not configured
        if (string.IsNullOrEmpty(_options.Host))
            return metrics;

        // Only collect once per day, at or after the configured hour (local PC time)
        var today = now.DateTime.Date;
        if (_lastReportDate == today)
            return metrics;

        if (now.DateTime.Hour < _options.ReportHourUtc)
            return metrics;

        _logger.LogInformation("Running Cognex DMCC daily report for {Date}", today);

        try
        {
            using var client = new TcpClient();
            client.ReceiveTimeout = _options.TimeoutSeconds * 1000;
            client.SendTimeout = _options.TimeoutSeconds * 1000;

            var connectTask = client.ConnectAsync(_options.Host, _options.Port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(_options.TimeoutSeconds * 1000, cancellationToken));

            if (completed != connectTask || !client.Connected)
            {
                metrics.Add(new Metric
                {
                    Id = "cognex.dmcc.connection",
                    Name = "Cognex DMCC Connection",
                    Value = false,
                    Source = "Cognex",
                    Timestamp = now,
                    Tags = new Dictionary<string, object> { { "host", _options.Host }, { "error", "Connection timeout" } }
                });
                return metrics;
            }

            using var stream = client.GetStream();

            // Login if credentials configured
            if (!string.IsNullOrEmpty(_options.Username))
            {
                await SendCommandAsync(stream, $"SET USER.NAME \"{_options.Username}\"", cancellationToken);
                await SendCommandAsync(stream, $"SET USER.PASSWORD \"{_options.Password}\"", cancellationToken);
            }

            // Get statistics
            var totalTriggers = await GetStatisticAsync(stream, "STATISTICS.TOTAL-TRIGGERS", cancellationToken);
            var totalReads = await GetStatisticAsync(stream, "STATISTICS.TOTAL-READS", cancellationToken);
            var noReads = await GetStatisticAsync(stream, "STATISTICS.NO-READS", cancellationToken);

            var readRate = totalTriggers > 0
                ? Math.Round((double)totalReads / totalTriggers * 100, 2)
                : 0.0;

            metrics.Add(new Metric
            {
                Id = "cognex.dmcc.daily_report",
                Name = "Cognex DMCC Daily Report",
                Value = new
                {
                    TotalTriggers = totalTriggers,
                    TotalReads = totalReads,
                    NoReads = noReads,
                    ReadRate = readRate
                },
                Unit = "counts",
                Source = "Cognex",
                Timestamp = now,
                Tags = new Dictionary<string, object>
                {
                    { "host", _options.Host },
                    { "report_date", today.ToString("yyyy-MM-dd") }
                }
            });

            metrics.Add(new Metric
            {
                Id = "cognex.dmcc.connection",
                Name = "Cognex DMCC Connection",
                Value = true,
                Source = "Cognex",
                Timestamp = now,
                Tags = new Dictionary<string, object> { { "host", _options.Host } }
            });

            _lastReportDate = today;
            _logger.LogInformation("Cognex daily report: Triggers={Triggers}, Reads={Reads}, NoReads={NoReads}, Rate={Rate}%",
                totalTriggers, totalReads, noReads, readRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting Cognex DMCC report from {Host}:{Port}", _options.Host, _options.Port);
            metrics.Add(new Metric
            {
                Id = "cognex.dmcc.connection",
                Name = "Cognex DMCC Connection",
                Value = false,
                Source = "Cognex",
                Timestamp = now,
                Tags = new Dictionary<string, object>
                {
                    { "host", _options.Host ?? "" },
                    { "error", ex.Message }
                }
            });
        }

        return metrics;
    }

    private async Task<int> GetStatisticAsync(NetworkStream stream, string statName, CancellationToken cancellationToken)
    {
        var response = await SendCommandAsync(stream, $"GET {statName}", cancellationToken);

        // DMCC success response format: "1 <value>\r\n"
        if (response.StartsWith("1 "))
        {
            var valueStr = response[2..].Trim();
            if (int.TryParse(valueStr, out var value))
                return value;
        }

        _logger.LogWarning("Unexpected DMCC response for {Stat}: {Response}", statName, response);
        return 0;
    }

    private static async Task<string> SendCommandAsync(NetworkStream stream, string command, CancellationToken cancellationToken)
    {
        var cmdBytes = Encoding.ASCII.GetBytes(command + "\r\n");
        await stream.WriteAsync(cmdBytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);

        var buffer = new byte[4096];
        var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
        return Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
    }
}
