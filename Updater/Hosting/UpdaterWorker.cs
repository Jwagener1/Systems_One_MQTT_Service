using Microsoft.Extensions.Options;
using Systems_One_MQTT_Updater.GitHub;
using Systems_One_MQTT_Updater.Reporting;
using Systems_One_MQTT_Updater.Scheduling;
using Systems_One_MQTT_Updater.Update;

namespace Systems_One_MQTT_Updater.Hosting;

public class UpdaterWorker : BackgroundService
{
    private readonly ReleaseChecker _releaseChecker;
    private readonly UpdateDownloader _downloader;
    private readonly UpdateApplicator _applicator;
    private readonly QuietWindowAnalyzer _quietWindow;
    private readonly ActivityChecker _activityChecker;
    private readonly UpdaterMqttReporter _reporter;
    private readonly UpdaterSettings _settings;
    private readonly ILogger<UpdaterWorker> _logger;

    // State held in memory
    private StagedUpdate? _staged;
    private DateTime _stagedAt = DateTime.MinValue;

    private record StagedUpdate(string ZipPath, ReleaseManifest Manifest);

    public UpdaterWorker(
        ReleaseChecker releaseChecker,
        UpdateDownloader downloader,
        UpdateApplicator applicator,
        QuietWindowAnalyzer quietWindow,
        ActivityChecker activityChecker,
        UpdaterMqttReporter reporter,
        IOptions<UpdaterSettings> options,
        ILogger<UpdaterWorker> logger)
    {
        _releaseChecker  = releaseChecker;
        _downloader      = downloader;
        _applicator      = applicator;
        _quietWindow     = quietWindow;
        _activityChecker = activityChecker;
        _reporter        = reporter;
        _settings        = options.Value;
        _logger          = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("UpdaterWorker starting");
        await _reporter.ConnectAsync(cancellationToken);
        await _reporter.PublishStateAsync("idle", cancellationToken);

        // Seed quiet window analysis on startup (don't wait for midnight)
        await _quietWindow.RefreshAsync(cancellationToken);
        await _reporter.PublishNextWindowAsync(_quietWindow.PreferredUpdateHour, cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Add per-machine jitter (±10 min) so a large fleet doesn't all hit GitHub simultaneously
        var jitter = TimeSpan.FromSeconds(Random.Shared.Next(-600, 600));
        var pollInterval    = TimeSpan.FromHours(_settings.PollIntervalHours) + jitter;
        var gateCheckPeriod = TimeSpan.FromMinutes(5);

        using var pollTimer     = new PeriodicTimer(pollInterval);
        using var gateTimer     = new PeriodicTimer(gateCheckPeriod);
        using var refreshTimer  = new PeriodicTimer(TimeSpan.FromHours(24));

        // Run poll and gate checks concurrently
        var pollTask    = RunPollLoopAsync(pollTimer, stoppingToken);
        var gateTask    = RunGateLoopAsync(gateTimer, stoppingToken);
        var refreshTask = RunDailyRefreshLoopAsync(refreshTimer, stoppingToken);

        await Task.WhenAll(pollTask, gateTask, refreshTask);
    }

    // ─── Hourly poll: check GitHub, download + verify if new version found ─────

    private async Task RunPollLoopAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        // Run once immediately on startup, then on the timer
        await PollOnceAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
            await PollOnceAsync(stoppingToken);
    }

    private async Task PollOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _reporter.PublishStateAsync("checking", stoppingToken);
            await _reporter.PublishLastCheckAsync(stoppingToken);

            var manifest = await _releaseChecker.CheckForUpdateAsync(stoppingToken);

            if (manifest is null)
            {
                await _reporter.PublishStateAsync("idle", stoppingToken);
                return;
            }

            // Already have this version staged — don't re-download
            if (_staged?.Manifest.Version == manifest.Version)
            {
                _logger.LogDebug("Version {Version} already staged — skipping download", manifest.Version);
                return;
            }

            await _reporter.PublishStateAsync("downloading", stoppingToken);
            var zipPath = await _downloader.DownloadAndVerifyAsync(manifest, stoppingToken);

            _staged   = new StagedUpdate(zipPath, manifest);
            _stagedAt = DateTime.UtcNow;

            await _reporter.PublishStateAsync("staged", stoppingToken);
            _logger.LogInformation("Update {Version} staged and ready — waiting for quiet window (preferred hour: {Hour:00}:00)",
                manifest.Version, _quietWindow.PreferredUpdateHour);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during update poll");
            await _reporter.PublishErrorAsync(ex.Message, stoppingToken);
            await _reporter.PublishStateAsync("failed", stoppingToken);
        }
    }

    // ─── Every 5 min: check if now is the quiet window, apply if ready ─────────

    private async Task RunGateLoopAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await GateCheckOnceAsync(stoppingToken);
    }

    private async Task GateCheckOnceAsync(CancellationToken stoppingToken)
    {
        if (_staged is null) return;

        var now = DateTime.Now;

        // Safety valve: if deferred too long, force at next midnight regardless
        bool forcedApply = (DateTime.UtcNow - _stagedAt).TotalDays >= _settings.MaxDeferDays
                           && now.Hour == 0;

        bool inWindow = _settings.BypassQuietWindow
                        || now.Hour == _quietWindow.PreferredUpdateHour
                        || forcedApply;

        if (!inWindow) return;

        if (forcedApply)
            _logger.LogWarning("Update {Version} deferred {Days} days — forcing application at midnight",
                _staged.Manifest.Version, _settings.MaxDeferDays);

        bool isQuiet = forcedApply || await _activityChecker.IsQuietAsync(_quietWindow.HourlyAverages, stoppingToken);

        if (!isQuiet)
        {
            _logger.LogDebug("In quiet window but line is still active — will retry in 5 min");
            return;
        }

        await ApplyUpdateAsync(_staged, stoppingToken);
    }

    private async Task ApplyUpdateAsync(StagedUpdate update, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Applying update {Version}", update.Manifest.Version);
        await _reporter.PublishStateAsync("applying", stoppingToken);

        try
        {
            var result = await _applicator.ApplyAsync(update.ZipPath, update.Manifest.Version, stoppingToken);

            _staged = null;

            switch (result)
            {
                case ApplyResult.Success:
                    await _reporter.PublishVersionAsync(update.Manifest.Version, stoppingToken);
                    await _reporter.PublishStateAsync("success", stoppingToken);
                    _logger.LogInformation("Update {Version} applied successfully", update.Manifest.Version);
                    break;

                case ApplyResult.RolledBack:
                    await _reporter.PublishStateAsync("rolled_back", stoppingToken);
                    await _reporter.PublishErrorAsync(
                        $"Update {update.Manifest.Version} failed health check — rolled back", stoppingToken);
                    _logger.LogError("Update {Version} rolled back", update.Manifest.Version);
                    break;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _staged = null;
            _logger.LogError(ex, "Unhandled error applying update {Version}", update.Manifest.Version);
            await _reporter.PublishStateAsync("failed", stoppingToken);
            await _reporter.PublishErrorAsync(ex.Message, stoppingToken);
        }
    }

    // ─── Daily at midnight: refresh quiet window analysis ───────────────────────

    private async Task RunDailyRefreshLoopAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await _quietWindow.RefreshAsync(stoppingToken);
                await _reporter.PublishNextWindowAsync(_quietWindow.PreferredUpdateHour, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Daily quiet window refresh failed");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("UpdaterWorker stopping");
        await base.StopAsync(cancellationToken);
        await _reporter.DisposeAsync();
    }
}
