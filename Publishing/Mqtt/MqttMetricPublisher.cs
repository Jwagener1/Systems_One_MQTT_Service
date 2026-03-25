using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Exceptions;
using MQTTnet.Protocol;
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
    private MqttClientOptions? _clientOptions;
    private readonly ConcurrentQueue<Metric> _buffer = new();
    private readonly SemaphoreSlim _reconnectLock = new(1, 1);
    private const int MaxBufferSize = 1000;

    public bool IsConnected => _client?.IsConnected == true;

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

            _client.DisconnectedAsync += args =>
            {
                _logger.LogWarning("MQTT connection lost: {Reason}", args.Reason);
                return Task.CompletedTask;
            };

            var statusTopic = BuildStatusTopic();

            var builder = new MqttClientOptionsBuilder()
                .WithTcpServer(_settings.BrokerUrl?.Replace("mqtt://", string.Empty) ?? "localhost", _settings.BrokerPort)
                .WithClientId(_settings.ClientId ?? Environment.MachineName)
                .WithWillTopic(statusTopic)
                .WithWillPayload("offline")
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(30));

            if (!string.IsNullOrEmpty(_settings.Username))
                builder = builder.WithCredentials(_settings.Username, _settings.Password);

            _clientOptions = builder.Build();

            await ConnectWithRetryAsync(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), cancellationToken);

            // Publish online status
            if (_client.IsConnected)
            {
                var onlineMessage = new MqttApplicationMessageBuilder()
                    .WithTopic(statusTopic)
                    .WithPayload("online")
                    .WithRetainFlag(true)
                    .Build();
                await _client.PublishAsync(onlineMessage, cancellationToken);
            }
        }
    }

    public async Task PublishAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken = default)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["Component"] = nameof(MqttMetricPublisher) }))
        {
            // If disconnected, try to reconnect
            if (_client == null || !_client.IsConnected)
            {
                _logger.LogWarning("MQTT client disconnected. Attempting reconnect...");
                var reconnected = await TryReconnectAsync(cancellationToken);
                if (!reconnected)
                {
                    // Buffer metrics for later
                    BufferMetrics(metrics);
                    return;
                }
            }

            // Drain buffer first
            await DrainBufferAsync(cancellationToken);

            // Publish current metrics
            var machine = Environment.MachineName;
            var count = 0;
            foreach (var metric in metrics)
            {
                try
                {
                    await PublishSingleMetricAsync(machine, metric, cancellationToken);
                    count++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to publish metric {MetricId}", metric.Id);
                    _buffer.Enqueue(metric);
                    TrimBuffer();
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
                    var offlineMessage = new MqttApplicationMessageBuilder()
                        .WithTopic(BuildStatusTopic())
                        .WithPayload("offline")
                        .WithRetainFlag(true)
                        .Build();
                    await _client.PublishAsync(offlineMessage, cancellationToken);
                    await _client.DisconnectAsync();
                    _logger.LogInformation("Disconnected from MQTT broker");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during MQTT disconnect");
                }
            }

            _reconnectLock.Dispose();
        }
    }

    private async Task ConnectWithRetryAsync(int maxAttempts, TimeSpan initialDelay, TimeSpan maxDelay, CancellationToken cancellationToken)
    {
        var attempt = 0;
        var delay = initialDelay;
        Exception? lastError = null;

        _logger.LogInformation("Connecting to MQTT broker {Broker}:{Port}", _settings.BrokerUrl, _settings.BrokerPort);

        while (attempt < maxAttempts && (_client?.IsConnected != true))
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempt++;

            try
            {
                await _client!.ConnectAsync(_clientOptions, cancellationToken);
                if (_client.IsConnected)
                {
                    _logger.LogInformation("Connected to MQTT broker on attempt {Attempt}", attempt);
                    return;
                }
            }
            catch (SocketException ex)
            {
                lastError = ex;
                _logger.LogWarning(ex, "Socket error on MQTT connect attempt {Attempt}", attempt);
            }
            catch (MqttCommunicationException ex)
            {
                lastError = ex;
                _logger.LogWarning(ex, "MQTT communication error on attempt {Attempt}", attempt);
            }
            catch (Exception ex)
            {
                lastError = ex;
                _logger.LogError(ex, "Unexpected error on MQTT connect attempt {Attempt}", attempt);
            }

            if (attempt >= maxAttempts)
                break;

            var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, (int)(delay.TotalMilliseconds * 0.2)));
            var backoff = delay + jitter;
            if (backoff > maxDelay) backoff = maxDelay;

            await Task.Delay(backoff, cancellationToken);
            delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, maxDelay.TotalMilliseconds));
        }

        _logger.LogError(lastError, "Failed to connect to MQTT broker after {MaxAttempts} attempts", maxAttempts);
        throw new MqttCommunicationException($"Failed to connect after {maxAttempts} attempts.", lastError);
    }

    private async Task<bool> TryReconnectAsync(CancellationToken cancellationToken)
    {
        if (!await _reconnectLock.WaitAsync(0, cancellationToken))
        {
            // Another reconnect in progress — just buffer
            return _client?.IsConnected == true;
        }

        try
        {
            if (_client?.IsConnected == true)
                return true;

            if (_clientOptions == null || _client == null)
                return false;

            try
            {
                await ConnectWithRetryAsync(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), cancellationToken);

                if (_client.IsConnected)
                {
                    _logger.LogInformation("MQTT reconnected successfully");
                    var onlineMessage = new MqttApplicationMessageBuilder()
                        .WithTopic(BuildStatusTopic())
                        .WithPayload("online")
                        .WithRetainFlag(true)
                        .Build();
                    await _client.PublishAsync(onlineMessage, cancellationToken);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MQTT reconnection failed");
            }

            return false;
        }
        finally
        {
            _reconnectLock.Release();
        }
    }

    private async Task PublishSingleMetricAsync(string machine, Metric metric, CancellationToken cancellationToken)
    {
        var topic = MqttTopicBuilder.Build(_settings.BaseTopic, machine, metric.Source, metric.Id);
        var payload = JsonSerializer.Serialize(new
        {
            metric.Id,
            metric.Name,
            metric.Value,
            metric.Unit,
            Timestamp = metric.Timestamp.ToUnixTimeSeconds(),
            metric.Source,
            metric.Tags
        });

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _client!.PublishAsync(message, cancellationToken);
        _logger.LogDebug("Published {MetricId} to {Topic}", metric.Id, topic);
    }

    private async Task DrainBufferAsync(CancellationToken cancellationToken)
    {
        if (_buffer.IsEmpty) return;

        var drained = 0;
        while (_buffer.TryDequeue(out var buffered))
        {
            try
            {
                await PublishSingleMetricAsync(Environment.MachineName, buffered, cancellationToken);
                drained++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish buffered metric {MetricId}", buffered.Id);
                break; // Stop draining if publish fails
            }
        }

        if (drained > 0)
            _logger.LogInformation("Drained {Count} buffered metrics", drained);
    }

    private void BufferMetrics(IEnumerable<Metric> metrics)
    {
        foreach (var metric in metrics)
        {
            _buffer.Enqueue(metric);
        }
        TrimBuffer();
        _logger.LogWarning("Buffered metrics. Queue size: {Size}", _buffer.Count);
    }

    private void TrimBuffer()
    {
        while (_buffer.Count > MaxBufferSize)
            _buffer.TryDequeue(out _);
    }

    private string BuildStatusTopic() =>
        string.Join('/', _settings.BaseTopic, Environment.MachineName, "status");
}
