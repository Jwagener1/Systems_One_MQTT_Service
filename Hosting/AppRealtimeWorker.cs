using Systems_One_MQTT_Service.Abstractions;

namespace Systems_One_MQTT_Service.Hosting
{
    public class AppRealtimeWorker : BackgroundService
    {
        private readonly ILogger<AppRealtimeWorker> _logger;
        private readonly IEnumerable<IMetricCollector> _collectors;
        private readonly IMetricPublisher _publisher;
        private const int IntervalMs = 2000;

        public AppRealtimeWorker(ILogger<AppRealtimeWorker> logger, IEnumerable<IMetricCollector> collectors, IMetricPublisher publisher)
        {
            _logger = logger;
            _collectors = collectors;
            _publisher = publisher;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var appCollectors = _collectors.Where(c => string.Equals(c.Category, "App", StringComparison.OrdinalIgnoreCase)).ToList();
            if (appCollectors.Count == 0)
            {
                _logger.LogWarning("No App collectors registered — realtime monitoring disabled");
                return;
            }

            _logger.LogInformation("AppRealtimeWorker started — {Count} app collectors, {IntervalMs}ms interval", appCollectors.Count, IntervalMs);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    foreach (var collector in appCollectors)
                    {
                        _logger.LogTrace("Realtime poll: {CollectorName}", collector.Name);
                        var metrics = await collector.CollectAsync(stoppingToken);
                        var list = metrics.ToList();
                        if (list.Count > 0)
                        {
                            _logger.LogDebug("Realtime: {Count} metrics from {CollectorName}", list.Count, collector.Name);
                            await _publisher.PublishAsync(list, stoppingToken);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in app realtime loop");
                }

                await Task.Delay(IntervalMs, stoppingToken);
            }

            _logger.LogInformation("AppRealtimeWorker stopped");
        }
    }
}
