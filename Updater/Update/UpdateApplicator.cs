using System.IO.Compression;
using System.ServiceProcess;
using Microsoft.Extensions.Options;

namespace Systems_One_MQTT_Updater.Update;

public enum ApplyResult { Success, FailedRetryExhausted, RolledBack }

public class UpdateApplicator
{
    private readonly UpdaterSettings _settings;
    private readonly ILogger<UpdateApplicator> _logger;

    private const int ServiceStopTimeoutSeconds  = 60;
    private const int ServiceStartTimeoutSeconds = 90;
    private const int HealthCheckWaitSeconds     = 30;

    public UpdateApplicator(IOptions<UpdaterSettings> options, ILogger<UpdateApplicator> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Applies the downloaded ZIP to the main service install directory.
    /// Attempt 1: stop → backup → extract → start → health check.
    /// Attempt 2 (on failure): stop → re-extract → start → health check.
    /// Rollback: stop → restore backup → start.
    /// </summary>
    public async Task<ApplyResult> ApplyAsync(string zipPath, string version, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying update {Version} from {Zip}", version, zipPath);

        Directory.CreateDirectory(_settings.StagingDir);
        Directory.CreateDirectory(_settings.BackupDir);

        // Attempt 1
        StopMainService();
        BackupCurrentInstall();
        ExtractUpdate(zipPath);
        StartMainService();

        if (await WaitForHealthyAsync(cancellationToken))
        {
            _logger.LogInformation("Update {Version} applied successfully on first attempt", version);
            CleanupWorkDirs();
            return ApplyResult.Success;
        }

        _logger.LogWarning("Service unhealthy after first attempt — retrying");

        // Attempt 2
        StopMainService();
        ExtractUpdate(zipPath);
        StartMainService();

        if (await WaitForHealthyAsync(cancellationToken))
        {
            _logger.LogInformation("Update {Version} applied successfully on second attempt", version);
            CleanupWorkDirs();
            return ApplyResult.Success;
        }

        _logger.LogError("Service still unhealthy after second attempt — rolling back to previous version");

        Rollback();

        return ApplyResult.RolledBack;
    }

    private void StopMainService()
    {
        using var sc = new ServiceController(_settings.MainServiceName);
        if (sc.Status == ServiceControllerStatus.Stopped)
        {
            _logger.LogDebug("Main service already stopped");
            return;
        }

        _logger.LogInformation("Stopping {ServiceName}", _settings.MainServiceName);
        sc.Stop();
        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(ServiceStopTimeoutSeconds));
        _logger.LogDebug("Main service stopped");
    }

    private void StartMainService()
    {
        using var sc = new ServiceController(_settings.MainServiceName);
        if (sc.Status == ServiceControllerStatus.Running)
        {
            _logger.LogDebug("Main service already running");
            return;
        }

        _logger.LogInformation("Starting {ServiceName}", _settings.MainServiceName);
        sc.Start();
        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(ServiceStartTimeoutSeconds));
        _logger.LogDebug("Main service started");
    }

    private void BackupCurrentInstall()
    {
        if (Directory.Exists(_settings.BackupDir))
            Directory.Delete(_settings.BackupDir, recursive: true);
        Directory.CreateDirectory(_settings.BackupDir);

        CopyDirectory(_settings.MainServiceInstallDir, _settings.BackupDir, skipAppsettings: false);
        _logger.LogDebug("Backed up current install to {BackupDir}", _settings.BackupDir);
    }

    private void ExtractUpdate(string zipPath)
    {
        if (Directory.Exists(_settings.StagingDir))
            Directory.Delete(_settings.StagingDir, recursive: true);
        Directory.CreateDirectory(_settings.StagingDir);

        _logger.LogDebug("Extracting {Zip} to staging dir", zipPath);
        ZipFile.ExtractToDirectory(zipPath, _settings.StagingDir, overwriteFiles: true);

        // Copy from staging → install dir, never overwriting appsettings.json
        CopyDirectory(_settings.StagingDir, _settings.MainServiceInstallDir, skipAppsettings: true);
        _logger.LogDebug("Staged files copied to install dir");
    }

    private void Rollback()
    {
        _logger.LogWarning("Rolling back to backed-up version");
        StopMainService();
        CopyDirectory(_settings.BackupDir, _settings.MainServiceInstallDir, skipAppsettings: false);
        StartMainService();
        _logger.LogInformation("Rollback complete");
    }

    private async Task<bool> WaitForHealthyAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Waiting {Seconds}s for service to stabilise", HealthCheckWaitSeconds);
        await Task.Delay(TimeSpan.FromSeconds(HealthCheckWaitSeconds), cancellationToken);

        try
        {
            using var sc = new ServiceController(_settings.MainServiceName);
            sc.Refresh();
            var isRunning = sc.Status == ServiceControllerStatus.Running;
            _logger.LogDebug("Health check: service status = {Status}", sc.Status);
            return isRunning;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed to query service status");
            return false;
        }
    }

    private static void CopyDirectory(string source, string dest, bool skipAppsettings)
    {
        Directory.CreateDirectory(dest);

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            if (skipAppsettings &&
                string.Equals(Path.GetFileName(file), "appsettings.json", StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = Path.GetRelativePath(source, file);
            var target   = Path.Combine(dest, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private void CleanupWorkDirs()
    {
        foreach (var dir in new[] { _settings.StagingDir, _settings.DownloadsDir })
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Cleanup of {Dir} failed — non-fatal", dir);
            }
        }
    }
}
