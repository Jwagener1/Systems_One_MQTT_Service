using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace Systems_One_MQTT_Updater.Reporting;

public class UpdaterMqttReporter : IAsyncDisposable
{
    private readonly IConfiguration _config;
    private readonly ILogger<UpdaterMqttReporter> _logger;
    private IMqttClient? _client;
    private string? _topicBase;

    public UpdaterMqttReporter(IConfiguration config, ILogger<UpdaterMqttReporter> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var brokerUrl  = _config["Mqtt:BrokerUrl"]?.Trim() ?? "mqtt://localhost";
        var company    = _config["Mqtt:Company"] ?? string.Empty;
        var location   = _config["Mqtt:Location"] ?? string.Empty;
        var machineId  = _config["Mqtt:MachineId"] ?? string.Empty;
        var baseTopic  = _config["Mqtt:BaseTopic"] ?? "systems-one";
        var username   = _config["Mqtt:Username"];
        var password   = _config["Mqtt:Password"];

        _topicBase = string.Join('/', baseTopic, company, location, machineId, "updater")
                           .TrimEnd('/');

        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        var isWebSocket = brokerUrl.StartsWith("ws://") || brokerUrl.StartsWith("wss://");
        var port = _config.GetValue<int>("Mqtt:BrokerPort", 0);

        MqttClientOptionsBuilder builder;
        if (isWebSocket)
        {
            var cleanUrl = brokerUrl.Replace("ws://", "").Replace("wss://", "");
            var p = port > 0 ? port : 80;
            builder = new MqttClientOptionsBuilder()
                .WithWebSocketServer(o => o.WithUri($"ws://{cleanUrl}:{p}{_config["Mqtt:BasePath"] ?? ""}"));
        }
        else
        {
            var cleanUrl = brokerUrl.Replace("mqtt://", "").Replace("mqtts://", "");
            var p = port > 0 ? port : 1883;
            builder = new MqttClientOptionsBuilder().WithTcpServer(cleanUrl, p);
        }

        builder = builder.WithClientId($"{company}_{location}_{machineId}_updater")
                         .WithKeepAlivePeriod(TimeSpan.FromMinutes(8));

        if (!string.IsNullOrEmpty(username))
            builder = builder.WithCredentials(username, password);

        try
        {
            await _client.ConnectAsync(builder.Build(), cancellationToken);
            _logger.LogInformation("Updater MQTT reporter connected to {Broker}", brokerUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Updater MQTT reporter failed to connect — status reporting disabled");
        }
    }

    public async Task PublishStateAsync(string state, CancellationToken cancellationToken = default)
        => await PublishAsync("state", state, retained: true, cancellationToken);

    public async Task PublishVersionAsync(string version, CancellationToken cancellationToken = default)
        => await PublishAsync("version", version, retained: true, cancellationToken);

    public async Task PublishLastCheckAsync(CancellationToken cancellationToken = default)
        => await PublishAsync("last_check", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), retained: true, cancellationToken);

    public async Task PublishNextWindowAsync(int hour, CancellationToken cancellationToken = default)
        => await PublishAsync("next_window", $"{hour:00}:00", retained: true, cancellationToken);

    public async Task PublishErrorAsync(string message, CancellationToken cancellationToken = default)
        => await PublishAsync("error", message, retained: false, cancellationToken);

    private async Task PublishAsync(string subtopic, string payload, bool retained, CancellationToken cancellationToken)
    {
        if (_client is not { IsConnected: true } || _topicBase is null)
            return;

        var topic = $"{_topicBase}/{subtopic}";
        try
        {
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(Encoding.UTF8.GetBytes(payload))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(retained)
                .Build();

            await _client.PublishAsync(msg, cancellationToken);
            _logger.LogTrace("Reported → {Topic}: {Payload}", topic, payload);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to publish updater status to {Topic}", topic);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is { IsConnected: true })
        {
            try { await _client.DisconnectAsync(); }
            catch { /* best effort */ }
        }
        _client?.Dispose();
    }
}
