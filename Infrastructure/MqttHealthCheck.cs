using Microsoft.Extensions.Diagnostics.HealthChecks;
using Systems_One_MQTT_Service.Publishing.Mqtt;

namespace Systems_One_MQTT_Service.Infrastructure;

/// <summary>
/// Health check that reports MQTT broker connectivity.
/// </summary>
public class MqttHealthCheck : IHealthCheck
{
    private readonly MqttMetricPublisher _publisher;

    public MqttHealthCheck(MqttMetricPublisher publisher)
    {
        _publisher = publisher;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_publisher.IsConnected)
        {
            return Task.FromResult(HealthCheckResult.Healthy("MQTT broker connected"));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy("MQTT broker disconnected"));
    }
}
