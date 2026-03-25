using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.OS;

/// <summary>
/// Collects PC temperature metrics from thermal zones.
/// On Windows: requires admin for WMI root\WMI, falls back to perf counters.
/// On Linux: reads /sys/class/thermal/thermal_zone*/temp.
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
        var now = _clock.UtcNow;

        try
        {
            List<TempReading> readings;

            if (OperatingSystem.IsWindows())
            {
                readings = await GetWindowsTemperaturesAsync();
            }
            else if (OperatingSystem.IsLinux())
            {
                readings = await GetLinuxTemperaturesAsync();
            }
            else
            {
                _logger.LogDebug("Temperature monitoring not supported on this platform");
                return metrics;
            }

            if (readings.Count > 0)
            {
                var avg = readings.Average(r => r.Temperature);
                var max = readings.Max(r => r.Temperature);

                metrics.Add(new Metric
                {
                    Id = "temperature",
                    Name = "System Temperature",
                    Value = new
                    {
                        averageC = Math.Round(avg, 1),
                        maxC = Math.Round(max, 1),
                        sensorCount = readings.Count,
                        status = GetStatus(max),
                        sensors = readings.Select(r => new { r.Name, temperatureC = r.Temperature, r.Source }).ToList()
                    },
                    Unit = "°C",
                    Source = "OS",
                    Timestamp = now
                });

                _logger.LogDebug("Temperature: avg={Avg:F1}°C, max={Max:F1}°C from {Count} sensors ({Status})",
                    avg, max, readings.Count, GetStatus(max));
            }
            else
            {
                _logger.LogDebug("No temperature sensors detected");
                metrics.Add(new Metric
                {
                    Id = "temperature",
                    Name = "System Temperature",
                    Value = new
                    {
                        averageC = (double?)null,
                        maxC = (double?)null,
                        sensorCount = 0,
                        status = "unavailable",
                        sensors = Array.Empty<object>()
                    },
                    Unit = "°C",
                    Source = "OS",
                    Timestamp = now
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting temperature metrics");
        }

        return metrics;
    }

    private async Task<List<TempReading>> GetWindowsTemperaturesAsync()
    {
        var readings = new List<TempReading>();

        await Task.Run(() =>
        {
            // Method 1: MSAcpi_ThermalZoneTemperature (requires admin/elevated)
            try
            {
                _logger.LogTrace("Trying MSAcpi_ThermalZoneTemperature (root\\WMI)");
                using var searcher = new System.Management.ManagementObjectSearcher("root\\WMI",
                    "SELECT * FROM MSAcpi_ThermalZoneTemperature");
                foreach (System.Management.ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        var temp = obj["CurrentTemperature"];
                        if (temp == null) continue;
                        var celsius = (Convert.ToDouble(temp) / 10.0) - 273.15;
                        if (celsius is > -50 and < 150)
                        {
                            var name = obj["InstanceName"]?.ToString() ?? "Thermal Zone";
                            readings.Add(new TempReading { Name = name, Temperature = Math.Round(celsius, 1), Source = "MSAcpi" });
                            _logger.LogTrace("MSAcpi sensor: {Name} = {Temp}°C", name, celsius);
                        }
                    }
                    catch (Exception ex) { _logger.LogTrace(ex, "Error reading MSAcpi thermal zone entry"); }
                }
            }
            catch (System.Management.ManagementException ex)
            {
                _logger.LogDebug("MSAcpi_ThermalZoneTemperature not available: {Message} (may need admin privileges)", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "MSAcpi_ThermalZoneTemperature query failed");
            }

            // Method 2: Win32_PerfRawData_Counters_ThermalZoneInformation (no admin required)
            if (readings.Count == 0)
            {
                try
                {
                    _logger.LogTrace("Trying Win32_PerfRawData_Counters_ThermalZoneInformation");
                    using var searcher = new System.Management.ManagementObjectSearcher("root\\CIMV2",
                        "SELECT * FROM Win32_PerfRawData_Counters_ThermalZoneInformation");
                    foreach (System.Management.ManagementObject obj in searcher.Get())
                    {
                        try
                        {
                            var temp = obj["Temperature"];
                            if (temp == null) continue;
                            var celsius = (Convert.ToDouble(temp) / 10.0) - 273.15;
                            if (celsius is > -50 and < 150)
                            {
                                var name = obj["Name"]?.ToString() ?? "Thermal Zone";
                                readings.Add(new TempReading { Name = name, Temperature = Math.Round(celsius, 1), Source = "PerfCounter" });
                                _logger.LogTrace("PerfCounter sensor: {Name} = {Temp}°C", name, celsius);
                            }
                        }
                        catch (Exception ex) { _logger.LogTrace(ex, "Error reading perf counter thermal entry"); }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "ThermalZoneInformation query failed");
                }
            }

            // Method 3: Win32_TemperatureProbe (rarely populated but worth trying)
            if (readings.Count == 0)
            {
                try
                {
                    _logger.LogTrace("Trying Win32_TemperatureProbe");
                    using var searcher = new System.Management.ManagementObjectSearcher(
                        "SELECT * FROM Win32_TemperatureProbe WHERE CurrentReading IS NOT NULL");
                    foreach (System.Management.ManagementObject obj in searcher.Get())
                    {
                        try
                        {
                            var temp = obj["CurrentReading"];
                            if (temp == null) continue;
                            var celsius = Convert.ToDouble(temp) / 10.0;
                            if (celsius is > -50 and < 150)
                            {
                                var name = obj["Name"]?.ToString() ?? "Temperature Probe";
                                readings.Add(new TempReading { Name = name, Temperature = Math.Round(celsius, 1), Source = "TemperatureProbe" });
                                _logger.LogTrace("TemperatureProbe sensor: {Name} = {Temp}°C", name, celsius);
                            }
                        }
                        catch (Exception ex) { _logger.LogTrace(ex, "Error reading temperature probe"); }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Win32_TemperatureProbe query failed");
                }
            }

            _logger.LogTrace("Windows temperature scan complete: {Count} sensors found", readings.Count);
        });

        return readings;
    }

    private async Task<List<TempReading>> GetLinuxTemperaturesAsync()
    {
        var readings = new List<TempReading>();

        await Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists("/sys/class/thermal")) return;

                foreach (var zone in Directory.GetDirectories("/sys/class/thermal", "thermal_zone*"))
                {
                    try
                    {
                        var tempFile = Path.Combine(zone, "temp");
                        if (!File.Exists(tempFile)) continue;

                        var text = File.ReadAllText(tempFile).Trim();
                        if (double.TryParse(text, out var milliCelsius))
                        {
                            var celsius = milliCelsius / 1000.0;
                            if (celsius is > -50 and < 150)
                            {
                                var name = Path.GetFileName(zone);
                                var typeFile = Path.Combine(zone, "type");
                                if (File.Exists(typeFile))
                                    name = File.ReadAllText(typeFile).Trim();

                                readings.Add(new TempReading { Name = name, Temperature = Math.Round(celsius, 1), Source = "sysfs" });
                                _logger.LogTrace("Linux sensor: {Name} = {Temp}°C", name, celsius);
                            }
                        }
                    }
                    catch (Exception ex) { _logger.LogTrace(ex, "Error reading thermal zone {Zone}", zone); }
                }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "Error enumerating Linux thermal zones"); }
        });

        return readings;
    }

    private static string GetStatus(double maxTemp) => maxTemp switch
    {
        < 50 => "Normal",
        < 70 => "Warm",
        < 85 => "Hot",
        _ => "Critical"
    };

    private class TempReading
    {
        public string Name { get; set; } = "";
        public double Temperature { get; set; }
        public string Source { get; set; } = "";
    }
}
