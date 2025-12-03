using Microsoft.Extensions.Options;
using Systems_One_MQTT_Service.Abstractions;

namespace Systems_One_MQTT_Service
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IEnumerable<IMetricCollector> _collectors;

        public Worker(ILogger<Worker> logger, IEnumerable<IMetricCollector> collectors)
        {
            _logger = logger;
            _collectors = collectors;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    foreach (var collector in _collectors)
                    {
                        _logger.LogInformation("Collecting metrics from: {CollectorName}", collector.Name);

                        var metrics = await collector.CollectAsync(stoppingToken);

                        foreach (var metric in metrics)
                        {
                            _logger.LogInformation(
                                "Metric: {MetricName} = {MetricValue} {Unit}",
                                metric.Name,
                                metric.Value,
                                metric.Unit ?? string.Empty);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error collecting metrics");
                }

                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
