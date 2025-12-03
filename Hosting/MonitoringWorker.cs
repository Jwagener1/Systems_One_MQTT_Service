using Systems_One_MQTT_Service.Abstractions;

namespace Systems_One_MQTT_Service.Hosting
{
    public class MonitoringWorker : BackgroundService
    {
        private readonly ILogger<MonitoringWorker> _logger;
        private readonly IEnumerable<IMetricCollector> _collectors;
        private readonly IMetricPublisher _publisher;
        private const int IntervalMs = 300000; // 5 minutes

        public MonitoringWorker(ILogger<MonitoringWorker> logger, IEnumerable<IMetricCollector> collectors, IMetricPublisher publisher)
        {
            _logger = logger;
            _collectors = collectors;
            _publisher = publisher;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            using (_logger.BeginScope(new Dictionary<string, object> { ["Component"] = nameof(MonitoringWorker) }))
            {
                _logger.LogInformation("MonitoringWorker starting. Collectors={CollectorCount}, PublisherType={PublisherType}", _collectors.Count(), _publisher.GetType().Name);
                await _publisher.ConnectAsync(cancellationToken);
                _logger.LogInformation("MonitoringWorker started");
            }
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            using (_logger.BeginScope(new Dictionary<string, object> { ["Component"] = nameof(MonitoringWorker) }))
            {
                _logger.LogInformation("MonitoringWorker stopping");
                await _publisher.DisconnectAsync(cancellationToken);
                _logger.LogInformation("MonitoringWorker stopped");
            }
            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (_logger.BeginScope(new Dictionary<string, object> { ["Component"] = nameof(MonitoringWorker) }))
            {
                _logger.LogInformation("Monitoring loop running with interval {IntervalMs}ms", IntervalMs);
                while (!stoppingToken.IsCancellationRequested)
                {
                    var loopStart = DateTimeOffset.UtcNow;
                    try
                    {
                        foreach (var collector in _collectors)
                        {
                            var start = DateTimeOffset.UtcNow;
                            _logger.LogInformation("Collecting from {CollectorName}", collector.Name);

                            var metrics = await collector.CollectAsync(stoppingToken);
                            var collectedCount = metrics.Count();
                            var durationMs = (int)(DateTimeOffset.UtcNow - start).TotalMilliseconds;

                            _logger.LogInformation("Collected {Count} metrics from {CollectorName} in {DurationMs}ms", collectedCount, collector.Name, durationMs);

                            foreach (var metric in metrics)
                            {
                                _logger.LogDebug(
                                    "Metric {Id} ({Name}) = {Value} {Unit} at {Timestamp}",
                                    metric.Id,
                                    metric.Name,
                                    metric.Value,
                                    metric.Unit ?? string.Empty,
                                    metric.Timestamp);
                            }

                            await _publisher.PublishAsync(metrics, stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in monitoring loop");
                    }

                    var elapsedMs = (int)(DateTimeOffset.UtcNow - loopStart).TotalMilliseconds;
                    var delayMs = Math.Max(0, IntervalMs - elapsedMs);
                    _logger.LogDebug("Loop elapsed {ElapsedMs}ms. Delaying {DelayMs}ms", elapsedMs, delayMs);
                    await Task.Delay(delayMs == 0 ? IntervalMs : delayMs, stoppingToken);
                }
                _logger.LogInformation("Monitoring loop cancelled");
            }
        }
    }
}
