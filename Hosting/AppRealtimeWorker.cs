using Systems_One_MQTT_Service.Abstractions;

namespace Systems_One_MQTT_Service.Hosting
{
    public class AppRealtimeWorker : BackgroundService
    {
        private readonly ILogger<AppRealtimeWorker> _logger;
        private readonly IEnumerable<IMetricCollector> _collectors;
        private readonly IMetricPublisher _publisher;
        private const int IntervalMs = 2000; // 2 seconds for near real-time

        public AppRealtimeWorker(ILogger<AppRealtimeWorker> logger, IEnumerable<IMetricCollector> collectors, IMetricPublisher publisher)
        {
            _logger = logger;
            _collectors = collectors;
            _publisher = publisher;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (_logger.BeginScope(new Dictionary<string, object> { ["Component"] = nameof(AppRealtimeWorker) }))
            {
                var appCollectors = _collectors.Where(c => string.Equals(c.Name, "App", StringComparison.OrdinalIgnoreCase)).ToList();
                if (appCollectors.Count == 0)
                {
                    _logger.LogWarning("No App collectors registered; realtime monitoring disabled");
                    return;
                }

                _logger.LogInformation("App realtime loop running with interval {IntervalMs}ms", IntervalMs);
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        foreach (var collector in appCollectors)
                        {
                            var metrics = await collector.CollectAsync(stoppingToken);
                            if (metrics.Any())
                            {
                                await _publisher.PublishAsync(metrics, stoppingToken);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in app realtime loop");
                    }

                    await Task.Delay(IntervalMs, stoppingToken);
                }
                _logger.LogInformation("App realtime loop cancelled");
            }
        }
    }
}
