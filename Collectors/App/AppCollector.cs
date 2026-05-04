using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.App;

public class AppCollector : IMetricCollector
{
    public string Name => "App";
    public string Category => "App";

    private readonly string _settingsDir;
    private readonly string _exePath;
    private readonly string _processName;
    private readonly ILogger<AppCollector> _logger;
    private readonly IClock _clock;

    private readonly object _stateLock = new();
    private bool? _lastRunning;
    private readonly ConcurrentDictionary<string, string> _lastSettingsHashes = new();

    public AppCollector(IOptions<AppCollectorOptions> options, ILogger<AppCollector> logger, IClock clock)
    {
        _logger = logger;
        _clock = clock;
        var opts = options.Value;
        _settingsDir = string.IsNullOrWhiteSpace(opts.SettingsDir)
            ? "C:/Users/Public/Documents/SystemOne_App_Settings"
            : opts.SettingsDir!;

        _exePath = string.IsNullOrWhiteSpace(opts.ExePath)
            ? "C:/Program Files/SystemsOne/StaticInstaller/Sys_One_Static_App.exe"
            : opts.ExePath!;

        _processName = Path.GetFileNameWithoutExtension(_exePath);
        _logger.LogDebug("AppCollector initialized: Process={ProcessName}, ExePath={ExePath}, SettingsDir={SettingsDir}",
            _processName, _exePath, _settingsDir);
    }

    public Task<IEnumerable<Metric>> CollectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("AppCollector.CollectAsync started");
        var metrics = new List<Metric>();
        var now = _clock.Now;

        var (isRunning, processCount, pathMatched) = GetProcessState(_processName, _exePath, _logger);
        _logger.LogTrace("Process state: Running={IsRunning}, Count={ProcessCount}, PathMatched={PathMatched}",
            isRunning, processCount, pathMatched);

        lock (_stateLock)
        {
            if (_lastRunning != isRunning)
            {
                _logger.LogInformation("App running state changed: {Old} -> {New}", _lastRunning, isRunning);
                _lastRunning = isRunning;
                metrics.Add(new Metric
                {
                    Id = "app.running",
                    Name = "App Running",
                    Value = isRunning,
                    Source = "App",
                    Timestamp = now,
                    Tags = new Dictionary<string, object>
                    {
                        { "process_name", _processName },
                        { "exe_path", _exePath },
                        { "process_count", processCount },
                        { "path_match", pathMatched }
                    }
                });
            }
        }

        try
        {
            if (Directory.Exists(_settingsDir))
            {
                _logger.LogTrace("Scanning settings directory: {Dir}", _settingsDir);
                foreach (var path in Directory.EnumerateFiles(_settingsDir, "*.json", SearchOption.TopDirectoryOnly))
                {
                    var key = Path.GetFileNameWithoutExtension(path);
                    _logger.LogTrace("Checking settings file: {Key} at {Path}", key, path);
                    using var fs = File.OpenRead(path);
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    var hashBytes = sha.ComputeHash(fs);
                    var hash = Convert.ToHexString(hashBytes);

                    var previousHash = _lastSettingsHashes.GetOrAdd(key, _ => string.Empty);
                    if (string.Equals(hash, previousHash, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogTrace("Settings file unchanged: {Key} (hash={Hash})", key, hash[..8]);
                        continue;
                    }

                    _lastSettingsHashes[key] = hash;

                    fs.Position = 0;
                    var doc = JsonDocument.Parse(fs);
                    metrics.Add(new Metric
                    {
                        Id = $"app.settings.{key}",
                        Name = $"{key} settings",
                        Value = doc.RootElement.Clone(),
                        Source = "App",
                        Timestamp = now,
                        Tags = new Dictionary<string, object>
                        {
                            { "path", path },
                            { "hash", hash }
                        }
                    });
                    _logger.LogDebug("Settings file changed: {Key} (hash={Hash})", key, hash[..8]);
                }
            }
            else
            {
                _logger.LogWarning("Settings directory missing: {Dir}", _settingsDir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning settings directory {Dir}", _settingsDir);
        }

        _logger.LogTrace("AppCollector.CollectAsync finished: {MetricCount} metrics", metrics.Count);
        return Task.FromResult<IEnumerable<Metric>>(metrics);
    }

    private static (bool isRunning, int processCount, bool pathMatched) GetProcessState(string processName, string exePath, ILogger logger)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            var count = processes.Length;
            if (count == 0)
            {
                logger.LogTrace("No processes found for {ProcessName}", processName);
                return (false, 0, false);
            }

            var matched = false;
            foreach (var p in processes)
            {
                try
                {
                    var path = p.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(path))
                    {
                        logger.LogTrace("Process PID {Pid}: path={Path}", p.Id, path);
                        if (string.Equals(path, exePath, StringComparison.OrdinalIgnoreCase))
                        {
                            matched = true;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Unable to read process module path for PID {Pid}", p.Id);
                }
            }

            return (matched || count > 0, count, matched);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking process state for {ProcessName}", processName);
            return (false, 0, false);
        }
    }
}
