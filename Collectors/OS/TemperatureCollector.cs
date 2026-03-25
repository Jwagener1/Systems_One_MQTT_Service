using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.OS;

/// <summary>
/// Collects CPU package temperature.
/// 
/// Priority:
///   1. LibreHardwareMonitor / OpenHardwareMonitor WMI (accurate CPU package temp)
///   2. Win32_PerfFormattedData_Counters_ThermalZoneInformation (ACPI zones, no admin)
///   3. Linux sysfs thermal zones
/// </summary>
public class TemperatureCollector : IMetricCollector
{
    public string Name => "Temperature";
    public string Category => "OS";

    private readonly IClock _clock;
    private readonly ILogger<TemperatureCollector> _logger;

    public TemperatureCollector(IClock clock, ILogger<TemperatureCollector> logger)
    {
        _clock = clock;
        _logger = logger;
    }

    public async Task<IEnumerable<Metric>> CollectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("TemperatureCollector.CollectAsync started");
        var metrics = new List<Metric>();

        try
        {
            double? tempCelsius = null;
            string source = "unknown";

            if (OperatingSystem.IsWindows())
            {
                // Try LibreHardwareMonitor/OpenHardwareMonitor first — gives actual CPU package temp
                (tempCelsius, source) = await GetHardwareMonitorTempAsync();

                // Fallback to ACPI thermal zones (less accurate, reads motherboard zones)
                if (!tempCelsius.HasValue)
                    (tempCelsius, source) = await GetThermalZoneTempAsync();
            }
            else if (OperatingSystem.IsLinux())
            {
                (tempCelsius, source) = await GetLinuxTempAsync();
            }

            if (tempCelsius.HasValue)
            {
                var status = tempCelsius.Value switch
                {
                    < 50 => "Normal",
                    < 70 => "Warm",
                    < 85 => "Hot",
                    _ => "Critical"
                };

                metrics.Add(new Metric
                {
                    Id = "temperature",
                    Name = "System Temperature",
                    Value = new { celsius = tempCelsius.Value, status, source },
                    Unit = "°C",
                    Source = "OS",
                    Timestamp = _clock.UtcNow
                });

                _logger.LogDebug("Temperature: {Temp}°C ({Status}) via {Source}", tempCelsius.Value, status, source);
            }
            else
            {
                metrics.Add(new Metric
                {
                    Id = "temperature",
                    Name = "System Temperature",
                    Value = new { celsius = (double?)null, status = "unavailable", source = "none" },
                    Unit = "°C",
                    Source = "OS",
                    Timestamp = _clock.UtcNow
                });
                _logger.LogDebug("Temperature: unavailable");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting temperature");
        }

        return metrics;
    }

    /// <summary>
    /// Reads CPU package temperature from LibreHardwareMonitor or OpenHardwareMonitor.
    /// These tools expose accurate per-sensor data via WMI when running.
    /// </summary>
    private async Task<(double? temp, string source)> GetHardwareMonitorTempAsync()
    {
        return await Task.Run(() =>
        {
            // Try both namespaces — LibreHardwareMonitor uses root\LibreHardwareMonitor,
            // OpenHardwareMonitor uses root\OpenHardwareMonitor
            var namespaces = new[] { "root\\LibreHardwareMonitor", "root\\OpenHardwareMonitor" };

            foreach (var ns in namespaces)
            {
                try
                {
                    _logger.LogTrace("Trying {Namespace} for CPU temperature", ns);

                    using var searcher = new System.Management.ManagementObjectSearcher(ns,
                        "SELECT Name, Value FROM Sensor WHERE SensorType='Temperature'");

                    double? cpuPackageTemp = null;
                    double? bestTemp = null;

                    foreach (System.Management.ManagementObject obj in searcher.Get())
                    {
                        var name = obj["Name"]?.ToString() ?? "";
                        var value = obj["Value"];
                        if (value == null) continue;

                        var celsius = Convert.ToDouble(value);
                        if (celsius is <= -50 or >= 150) continue;

                        _logger.LogTrace("{Namespace}: {Name} = {Temp}°C", ns, name, celsius);

                        // Prefer "CPU Package" or "Core (Tctl/Tdie)" — the overall CPU temp
                        if (name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Tdie", StringComparison.OrdinalIgnoreCase))
                        {
                            cpuPackageTemp = Math.Round(celsius, 1);
                        }

                        // Track highest CPU core temp as fallback
                        if (name.Contains("CPU", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                        {
                            bestTemp = Math.Max(bestTemp ?? 0, Math.Round(celsius, 1));
                        }
                    }

                    var result = cpuPackageTemp ?? bestTemp;
                    if (result.HasValue)
                    {
                        var sourceName = ns.Contains("Libre") ? "LibreHardwareMonitor" : "OpenHardwareMonitor";
                        _logger.LogDebug("CPU temp from {Source}: {Temp}°C", sourceName, result.Value);
                        return (result, sourceName);
                    }
                }
                catch (System.Management.ManagementException ex)
                {
                    _logger.LogTrace("{Namespace} not available: {Message}", ns, ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "{Namespace} query failed", ns);
                }
            }

            _logger.LogDebug("No hardware monitor WMI provider found");
            return ((double?)null, "none");
        });
    }

    /// <summary>
    /// Reads ACPI thermal zone temperature (no admin required).
    /// Note: these are typically motherboard/chipset temps, not CPU package.
    /// </summary>
    private async Task<(double? temp, string source)> GetThermalZoneTempAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger.LogTrace("Querying Win32_PerfFormattedData_Counters_ThermalZoneInformation");

                using var searcher = new System.Management.ManagementObjectSearcher("root\\CIMV2",
                    "SELECT Temperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation");

                double? maxTemp = null;

                foreach (System.Management.ManagementObject obj in searcher.Get())
                {
                    var raw = obj["Temperature"];
                    if (raw == null) continue;

                    var celsius = Convert.ToDouble(raw) - 273.15;
                    _logger.LogTrace("ACPI zone: {Temp}°C", Math.Round(celsius, 1));

                    if (celsius is > -50 and < 150)
                        maxTemp = Math.Max(maxTemp ?? 0, Math.Round(celsius, 1));
                }

                if (maxTemp.HasValue)
                    _logger.LogDebug("ACPI thermal zone max: {Temp}°C", maxTemp.Value);

                return (maxTemp, "ACPI");
            }
            catch (Exception ex)
            {
                _logger.LogDebug("ACPI thermal query failed: {Message}", ex.Message);
                return ((double?)null, "none");
            }
        });
    }

    private async Task<(double? temp, string source)> GetLinuxTempAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists("/sys/class/thermal"))
                    return ((double?)null, "none");

                double? maxTemp = null;

                foreach (var zone in Directory.GetDirectories("/sys/class/thermal", "thermal_zone*"))
                {
                    var tempFile = Path.Combine(zone, "temp");
                    if (!File.Exists(tempFile)) continue;

                    var text = File.ReadAllText(tempFile).Trim();
                    if (double.TryParse(text, out var milliCelsius))
                    {
                        var celsius = milliCelsius / 1000.0;
                        if (celsius is > -50 and < 150)
                            maxTemp = Math.Max(maxTemp ?? 0, Math.Round(celsius, 1));
                    }
                }

                return (maxTemp, "sysfs");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Linux thermal read failed");
                return ((double?)null, "none");
            }
        });
    }
}
