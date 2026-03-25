using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.PLC;

/// <summary>
/// Subscribes to a PLC MQTT broker for error messages and surfaces them as metrics.
/// </summary>
public class PlcErrorCollector : IMetricCollector, IDisposable
{
    public string Name => "PLC Errors";
    public string Category => "PLC";

    private readonly PlcErrorCollectorOptions _options;
    private readonly ILogger<PlcErrorCollector> _logger;
    private readonly IClock _clock;
    private IMqttClient? _client;
    private readonly ConcurrentQueue<PlcError> _errorQueue = new();
    private const int MaxQueueSize = 1000;
    private bool _connectAttempted;

    public PlcErrorCollector(IOptions<PlcErrorCollectorOptions> options, ILogger<PlcErrorCollector> logger, IClock clock)
    {
        _options = options.Value;
        _logger = logger;
        _clock = clock;
    }

    public async Task<IEnumerable<Metric>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var metrics = new List<Metric>();
        var now = _clock.UtcNow;

        // Ensure connected to PLC broker
        await EnsureConnectedAsync(cancellationToken);

        // Drain queue
        var errors = new List<PlcError>();
        while (_errorQueue.TryDequeue(out var error))
            errors.Add(error);

        if (errors.Count > 0)
        {
            // Summary grouped by type
            var grouped = errors.GroupBy(e => e.ErrorType).ToDictionary(
                g => g.Key,
                g => new { count = g.Count(), latest = g.Max(e => e.Timestamp) });

            metrics.Add(new Metric
            {
                Id = "plc.errors.summary",
                Name = "PLC Error Summary",
                Value = grouped,
                Source = "PLC",
                Timestamp = now,
                Tags = new Dictionary<string, object>
                {
                    { "total_errors", errors.Count },
                    { "unique_types", grouped.Count }
                }
            });

            // PE alignment errors specifically
            var peErrors = errors.Where(e =>
                e.ErrorType.Contains("alignment", StringComparison.OrdinalIgnoreCase) ||
                e.ErrorType.Contains("pe", StringComparison.OrdinalIgnoreCase) ||
                e.Topic.Contains("pe/alignment", StringComparison.OrdinalIgnoreCase)).ToList();

            if (peErrors.Count > 0)
            {
                metrics.Add(new Metric
                {
                    Id = "plc.pe_alignment.errors",
                    Name = "PE Alignment Errors",
                    Value = peErrors.Select(e => new
                    {
                        e.MachineId, e.ErrorType, e.ErrorCode, e.Message, e.Severity, e.Timestamp
                    }).ToList(),
                    Source = "PLC",
                    Timestamp = now,
                    Tags = new Dictionary<string, object>
                    {
                        { "pe_error_count", peErrors.Count },
                        { "has_critical", peErrors.Any(e => e.Severity == "Critical") }
                    }
                });
            }
        }

        // Connection status
        metrics.Add(new Metric
        {
            Id = "plc.connection.status",
            Name = "PLC MQTT Connection",
            Value = _client?.IsConnected == true,
            Source = "PLC",
            Timestamp = now,
            Tags = new Dictionary<string, object>
            {
                { "broker", _options.PlcMqttBrokerUrl ?? "not_configured" },
                { "queued_errors", _errorQueue.Count }
            }
        });

        return metrics;
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.PlcMqttBrokerUrl))
            return;

        if (_client?.IsConnected == true)
            return;

        try
        {
            if (_client == null)
            {
                _client = new MqttFactory().CreateMqttClient();
                _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
                _client.DisconnectedAsync += args =>
                {
                    _logger.LogWarning("PLC MQTT disconnected: {Reason}", args.Reason);
                    return Task.CompletedTask;
                };
            }

            var builder = new MqttClientOptionsBuilder()
                .WithTcpServer(_options.PlcMqttBrokerUrl.Replace("mqtt://", ""), _options.PlcMqttPort)
                .WithClientId($"systems-one-plc-{Environment.MachineName}")
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(30));

            if (!string.IsNullOrEmpty(_options.PlcMqttUsername))
                builder = builder.WithCredentials(_options.PlcMqttUsername, _options.PlcMqttPassword);

            await _client.ConnectAsync(builder.Build(), cancellationToken);

            foreach (var topic in _options.ErrorTopics)
            {
                await _client.SubscribeAsync(topic);
                _logger.LogInformation("Subscribed to PLC topic: {Topic}", topic);
            }

            _connectAttempted = true;
            _logger.LogInformation("Connected to PLC MQTT broker: {Broker}", _options.PlcMqttBrokerUrl);
        }
        catch (Exception ex)
        {
            if (!_connectAttempted)
            {
                _connectAttempted = true;
                _logger.LogWarning(ex, "Failed to connect to PLC MQTT broker: {Broker}", _options.PlcMqttBrokerUrl);
            }
            else
            {
                _logger.LogDebug(ex, "PLC MQTT reconnect failed");
            }
        }
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            var error = ParseError(topic, payload);

            _errorQueue.Enqueue(error);

            // Trim queue
            while (_errorQueue.Count > MaxQueueSize)
                _errorQueue.TryDequeue(out _);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error parsing PLC message");
        }
        return Task.CompletedTask;
    }

    private PlcError ParseError(string topic, string payload)
    {
        var error = new PlcError
        {
            Timestamp = _clock.UtcNow,
            Topic = topic,
            MachineId = topic.Split('/').FirstOrDefault() ?? "Unknown"
        };

        try
        {
            var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.TryGetProperty("error_type", out var et) || root.TryGetProperty("type", out et))
                error.ErrorType = et.GetString() ?? "Unknown";
            if (root.TryGetProperty("error_code", out var ec) || root.TryGetProperty("code", out ec))
                error.ErrorCode = ec.GetString() ?? "";
            if (root.TryGetProperty("message", out var msg) || root.TryGetProperty("description", out msg))
                error.Message = msg.GetString() ?? "";
            if (root.TryGetProperty("severity", out var sev) || root.TryGetProperty("level", out sev))
                error.Severity = sev.GetString() ?? "Warning";
        }
        catch (JsonException)
        {
            // Plain text payload
            error.ErrorType = "PlainText";
            error.Message = payload;
            error.Severity = topic.Contains("critical", StringComparison.OrdinalIgnoreCase) ? "Critical" : "Warning";
        }

        return error;
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
