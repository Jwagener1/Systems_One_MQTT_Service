using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.App;

public class AppCollector : IMetricCollector
{
    public string Name => "App";

    private readonly string _settingsDir;
    private readonly string _exePath;
    private readonly string _processName;
    private readonly ILogger<AppCollector> _logger;

    private bool? _lastRunning;
    private readonly Dictionary<string, string> _lastSettingsHashes = new();

    public AppCollector(IOptions<AppCollectorOptions> options, ILogger<AppCollector> logger)
    {
        _logger = logger;
        var opts = options.Value;
        _settingsDir = string.IsNullOrWhiteSpace(opts.SettingsDir)
            ? "C:/Users/Public/Documents/SystemOne_App_Settings"
            : opts.SettingsDir!;

        _exePath = string.IsNullOrWhiteSpace(opts.ExePath)
            ? "C:/Program Files/SystemsOne/StaticInstaller/Sys_One_Static_App.exe"
            : opts.ExePath!;

        _processName = Path.GetFileNameWithoutExtension(_exePath);
    }

    public Task<IEnumerable<Metric>> CollectAsync(CancellationToken cancellationToken = default)
    {
        using (_logger.BeginScope(new Dictionary<string, object> { ["Component"] = nameof(AppCollector) }))
        {
            var metrics = new List<Metric>();

            var (isRunning, processCount, pathMatched) = GetProcessState(_processName, _exePath, _logger);
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
                    Timestamp = DateTimeOffset.UtcNow,
                    Tags = new Dictionary<string, object>
                    {
                        { "process_name", _processName },
                        { "exe_path", _exePath },
                        { "process_count", processCount },
                        { "path_match", pathMatched }
                    }
                });
            }

            try
            {
                if (Directory.Exists(_settingsDir))
                {
                    foreach (var path in Directory.EnumerateFiles(_settingsDir, "*.json", SearchOption.TopDirectoryOnly))
                    {
                        var key = Path.GetFileNameWithoutExtension(path);
                        using var fs = File.OpenRead(path);
                        using var sha = System.Security.Cryptography.SHA256.Create();
                        var hashBytes = sha.ComputeHash(fs);
                        var hash = Convert.ToHexString(hashBytes);

                        if (!_lastSettingsHashes.TryGetValue(key, out var oldHash) || !string.Equals(hash, oldHash, StringComparison.OrdinalIgnoreCase))
                        {
                            fs.Position = 0;
                            var doc = JsonDocument.Parse(fs);
                            metrics.Add(new Metric
                            {
                                Id = $"app.settings.{key}",
                                Name = $"{key} settings",
                                Value = doc.RootElement.Clone(),
                                Source = "App",
                                Timestamp = DateTimeOffset.UtcNow,
                                Tags = new Dictionary<string, object>
                                {
                                    { "path", path },
                                    { "hash", hash }
                                }
                            });
                            _lastSettingsHashes[key] = hash;
                            _logger.LogInformation("Settings changed: {Key}", key);
                        }
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

            return Task.FromResult<IEnumerable<Metric>>(metrics);
        }
    }

    private static (bool isRunning, int processCount, bool pathMatched) GetProcessState(string processName, string exePath, ILogger logger)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);
            var count = processes.Length;
            if (count == 0)
                return (false, 0, false);

            var matched = false;
            foreach (var p in processes)
            {
                try
                {
                    var path = p.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(path))
                    {
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
