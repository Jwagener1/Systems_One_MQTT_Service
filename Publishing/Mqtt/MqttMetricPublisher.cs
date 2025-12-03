using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Exceptions;
using Microsoft.Extensions.Options;
using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Publishing.Mqtt;

public class MqttMetricPublisher : IMetricPublisher
{
    public string Name => "MQTT";

    private readonly MqttSettings _settings;
    private readonly ILogger<MqttMetricPublisher> _logger;
    private IMqttClient? _client;

    public MqttMetricPublisher(IOptions<MqttSettings> options, ILogger<MqttMetricPublisher> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["Component"] = nameof(MqttMetricPublisher) }))
        {
            var factory = new MqttFactory();
            _client = factory.CreateMqttClient();

            var builder = new MqttClientOptionsBuilder()
                .WithTcpServer(_settings.BrokerUrl?.Replace("mqtt://", string.Empty) ?? "localhost", _settings.BrokerPort)
                .WithClientId(_settings.ClientId ?? Environment.MachineName);

            if (!string.IsNullOrEmpty(_settings.Username))
                builder = builder.WithCredentials(_settings.Username, _settings.Password);

            var options = builder.Build();

            // Exponential backoff retry
            const int maxAttempts = 5;
            var attempt = 0;
            var delay = TimeSpan.FromSeconds(1);
            var maxDelay = TimeSpan.FromSeconds(30);
            Exception? lastError = null;

            _logger.LogInformation("Connecting to MQTT broker {Broker}:{Port} with ClientId {ClientId}",
                _settings.BrokerUrl, _settings.BrokerPort, _settings.ClientId ?? Environment.MachineName);

            while (attempt < maxAttempts && (_client?.IsConnected != true))
            {
                cancellationToken.ThrowIfCancellationRequested();
                attempt++;

                try
                {
                    _logger.LogDebug("MQTT connect attempt {Attempt}/{MaxAttempts}", attempt, maxAttempts);
                    await _client!.ConnectAsync(options, cancellationToken);
                    if (_client.IsConnected)
                    {
                        _logger.LogInformation("Connected to MQTT broker on attempt {Attempt}", attempt);
                        return;
                    }
                }
                catch (SocketException ex)
                {
                    lastError = ex;
                    _logger.LogWarning(ex, "Socket error connecting to MQTT broker (attempt {Attempt})", attempt);
                }
                catch (MqttCommunicationException ex)
                {
                    lastError = ex;
                    _logger.LogWarning(ex, "MQTT communication error (attempt {Attempt})", attempt);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    _logger.LogError(ex, "Unexpected error connecting to MQTT broker (attempt {Attempt})", attempt);
                }

                if (attempt >= maxAttempts)
                    break;

                // Backoff with jitter (up to 20%)
                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, (int)(delay.TotalMilliseconds * 0.2)));
                var backoff = delay + jitter;
                if (backoff > maxDelay)
                    backoff = maxDelay;

                _logger.LogDebug("Retrying MQTT connection after {BackoffMs} ms (next base delay {NextDelayMs} ms)",
                    (int)backoff.TotalMilliseconds, (int)Math.Min(delay.TotalMilliseconds * 2, maxDelay.TotalMilliseconds));

                await Task.Delay(backoff, cancellationToken);

                // Double delay for next attempt up to maxDelay
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, maxDelay.TotalMilliseconds));
            }

            // If we reach here, connection failed after retries
            _logger.LogError(lastError, "Failed to connect to MQTT broker after {MaxAttempts} attempts", maxAttempts);
            throw new MqttCommunicationException(
                $"Failed to connect to MQTT broker after {maxAttempts} attempts.",
                lastError);
        }
    }

    public async Task PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken = default)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["Component"] = nameof(MqttMetricPublisher) }))
        {
            if (_client is null || !_client.IsConnected)
            {
                _logger.LogWarning("Publish skipped: MQTT client not connected");
                return;
            }

            var machine = Environment.MachineName;
            var count = 0;
            foreach (var metric in metrics)
            {
                var topic = MqttTopicBuilder.Build(_settings.BaseTopic, machine, metric.Source, metric.Id);
                var payload = JsonSerializer.Serialize(new
                {
                    metric.Id,
                    metric.Name,
                    metric.Value,
                    metric.Unit,
                    metric.Timestamp,
                    metric.Source,
                    metric.Tags
                });

                var payloadSize = Encoding.UTF8.GetByteCount(payload);

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .Build();

                try
                {
                    await _client.PublishAsync(message, cancellationToken);
                    count++;
                    _logger.LogDebug("Published metric {MetricId} to topic {Topic} (size {PayloadSize} bytes)", metric.Id, topic, payloadSize);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish metric {MetricId} to topic {Topic}", metric.Id, topic);
                }
            }

            _logger.LogInformation("Published {Count} metrics via MQTT", count);
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["Component"] = nameof(MqttMetricPublisher) }))
        {
            if (_client is { IsConnected: true })
            {
                _logger.LogInformation("Disconnecting from MQTT broker");
                try
                {
                    await _client.DisconnectAsync();
                    _logger.LogInformation("Disconnected from MQTT broker");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during MQTT disconnect");
                }
            }
            else
            {
                _logger.LogDebug("Disconnect skipped: MQTT client not connected");
            }
        }
    }
}
