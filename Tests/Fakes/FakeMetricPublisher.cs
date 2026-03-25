using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Tests.Fakes;

public class FakeMetricPublisher : IMetricPublisher
{
    public List<Metric> Published { get; } = new();
    public bool IsConnected { get; private set; }
    public int ConnectCount { get; private set; }
    public int DisconnectCount { get; private set; }

    public Task ConnectAsync(CancellationToken ct = default) { ConnectCount++; IsConnected = true; return Task.CompletedTask; }
    public Task PublishAsync(IEnumerable<Metric> metrics, CancellationToken ct = default) { Published.AddRange(metrics); return Task.CompletedTask; }
    public Task DisconnectAsync(CancellationToken ct = default) { DisconnectCount++; IsConnected = false; return Task.CompletedTask; }
}
