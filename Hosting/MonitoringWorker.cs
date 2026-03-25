using Systems_One_MQTT_Service.Abstractions;

namespace Systems_One_MQTT_Service.Hosting;

public class MonitoringWorker : BackgroundService
{
    private readonly ILogger<MonitoringWorker> _logger;
    private readonly IEnumerable<IMetricCollector> _collectors;
    private readonly IMetricPublisher _publisher;
    private readonly IScheduler _scheduler;
    private readonly IClock _clock;
    private readonly TimeSpan _interval;

    public MonitoringWorker(
        ILogger<MonitoringWorker> logger,
        IEnumerable<IMetricCollector> collectors,
        IMetricPublisher publisher,
        IScheduler scheduler,
        IClock clock,
        IConfiguration configuration)
    {
        _logger = logger;
        _collectors = collectors;
        _publisher = publisher;
        _scheduler = scheduler;
        _clock = clock;

        var minutes = configuration.GetValue<int>("Monitoring:IntervalMinutes", 5);
        _interval = TimeSpan.FromMinutes(minutes);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MonitoringWorker starting — {CollectorCount} collectors, {IntervalMin}min interval",
            _collectors.Count(), _interval.TotalMinutes);

        try
        {
            await _publisher.ConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect publisher on startup — will retry on first collection cycle");
        }

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MonitoringWorker stopping");
        await base.StopAsync(cancellationToken);

        try
        {
            await _publisher.DisconnectAsync(cancellationToken);
            _logger.LogDebug("Publisher disconnected cleanly");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disconnecting publisher during shutdown");
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _scheduler.ScheduleAsync(async ct =>
        {
            var cycleStart = _clock.UtcNow;
            _logger.LogDebug("Collection cycle starting");

            foreach (var collector in _collectors)
            {
                var start = _clock.UtcNow;
                _logger.LogTrace("Running collector: {CollectorName} [{Category}]", collector.Name, collector.Category);

                try
                {
                    var metrics = await collector.CollectAsync(ct);
                    var list = metrics.ToList();
                    var durationMs = (int)(_clock.UtcNow - start).TotalMilliseconds;

                    _logger.LogDebug("{CollectorName}: {Count} metrics in {DurationMs}ms",
                        collector.Name, list.Count, durationMs);

                    if (list.Count > 0)
                        await _publisher.PublishAsync(list, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in collector {CollectorName}", collector.Name);
                }
            }

            var cycleDurationMs = (int)(_clock.UtcNow - cycleStart).TotalMilliseconds;
            _logger.LogDebug("Collection cycle complete: {CycleDurationMs}ms", cycleDurationMs);
        }, _interval, stoppingToken);
    }
}
