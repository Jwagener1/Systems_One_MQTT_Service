using Systems_One_MQTT_Service.Abstractions;

namespace Systems_One_MQTT_Service.Hosting;

public class MonitoringWorker : BackgroundService
{
    private readonly ILogger<MonitoringWorker> _logger;
    private readonly IEnumerable<IMetricCollector> _collectors;
    private readonly IMetricPublisher _publisher;
    private readonly IScheduler _scheduler;
    private readonly TimeSpan _interval;

    public MonitoringWorker(
        ILogger<MonitoringWorker> logger,
        IEnumerable<IMetricCollector> collectors,
        IMetricPublisher publisher,
        IScheduler scheduler,
        IConfiguration configuration)
    {
        _logger = logger;
        _collectors = collectors;
        _publisher = publisher;
        _scheduler = scheduler;

        var minutes = configuration.GetValue<int>("Monitoring:IntervalMinutes", 5);
        _interval = TimeSpan.FromMinutes(minutes);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "MonitoringWorker starting. Collectors={CollectorCount}, Interval={IntervalMin}min",
            _collectors.Count(), _interval.TotalMinutes);
        await _publisher.ConnectAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MonitoringWorker stopping");
        await _publisher.DisconnectAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _scheduler.ScheduleAsync(async ct =>
        {
            foreach (var collector in _collectors)
            {
                var start = DateTimeOffset.UtcNow;
                _logger.LogInformation("Collecting from {CollectorName}", collector.Name);

                var metrics = await collector.CollectAsync(ct);
                var list = metrics.ToList();
                var durationMs = (int)(DateTimeOffset.UtcNow - start).TotalMilliseconds;

                _logger.LogInformation(
                    "Collected {Count} metrics from {CollectorName} in {DurationMs}ms",
                    list.Count, collector.Name, durationMs);

                foreach (var metric in list)
                {
                    _logger.LogDebug(
                        "Metric {Id} ({Name}) = {Value} {Unit} at {Timestamp}",
                        metric.Id, metric.Name, metric.Value,
                        metric.Unit ?? string.Empty, metric.Timestamp);
                }

                await _publisher.PublishAsync(list, ct);
            }
        }, _interval, stoppingToken);
    }
}
