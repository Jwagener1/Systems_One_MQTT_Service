using System.Management;
using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.OS;

/// <summary>
/// Collects PC temperature metrics from thermal zones.
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
        var metrics = new List<Metric>();
        var now = _clock.UtcNow;

        try
        {
            var readings = OperatingSystem.IsWindows()
                ? await GetWindowsTemperaturesAsync()
                : await GetLinuxTemperaturesAsync();

            if (readings.Count > 0)
            {
                var avg = readings.Average(r => r.Temperature);
                var max = readings.Max(r => r.Temperature);

                metrics.Add(new Metric
                {
                    Id = "system.temperature.average",
                    Name = "Average System Temperature",
                    Value = Math.Round(avg, 1),
                    Unit = "°C",
                    Source = "OS",
                    Timestamp = now,
                    Tags = new Dictionary<string, object>
                    {
                        { "sensor_count", readings.Count },
                        { "max_temp", Math.Round(max, 1) },
                        { "status", GetStatus(max) }
                    }
                });

                metrics.Add(new Metric
                {
                    Id = "system.temperature.sensors",
                    Name = "Temperature Sensors",
                    Value = readings.Select(r => new { r.Name, r.Temperature, r.Source }).ToList(),
                    Unit = "°C",
                    Source = "OS",
                    Timestamp = now
                });
            }
            else
            {
                metrics.Add(new Metric
                {
                    Id = "system.temperature.status",
                    Name = "Temperature Monitoring",
                    Value = "No sensors detected",
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
            // Try MSAcpi_ThermalZoneTemperature (WMI root\WMI)
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
                foreach (ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        var temp = obj["CurrentTemperature"];
                        if (temp == null) continue;
                        var celsius = (Convert.ToDouble(temp) / 10.0) - 273.15;
                        if (celsius is > -50 and < 150)
                        {
                            readings.Add(new TempReading
                            {
                                Name = obj["InstanceName"]?.ToString() ?? "Thermal Zone",
                                Temperature = Math.Round(celsius, 1),
                                Source = "MSAcpi"
                            });
                        }
                    }
                    catch (Exception ex) { _logger.LogDebug(ex, "Error reading thermal zone entry"); }
                }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "MSAcpi_ThermalZoneTemperature not available"); }

            // Fallback: ThermalZoneInformation perf counter
            if (readings.Count == 0)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_PerfRawData_Counters_ThermalZoneInformation");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        try
                        {
                            var temp = obj["Temperature"];
                            if (temp == null) continue;
                            var celsius = (Convert.ToDouble(temp) / 10.0) - 273.15;
                            if (celsius is > -50 and < 150)
                            {
                                readings.Add(new TempReading
                                {
                                    Name = obj["Name"]?.ToString() ?? "Thermal Zone",
                                    Temperature = Math.Round(celsius, 1),
                                    Source = "PerfCounter"
                                });
                            }
                        }
                        catch (Exception ex) { _logger.LogDebug(ex, "Error reading perf counter thermal entry"); }
                    }
                }
                catch (Exception ex) { _logger.LogDebug(ex, "ThermalZoneInformation not available"); }
            }
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

                                readings.Add(new TempReading
                                {
                                    Name = name,
                                    Temperature = Math.Round(celsius, 1),
                                    Source = "sysfs"
                                });
                            }
                        }
                    }
                    catch (Exception ex) { _logger.LogDebug(ex, "Error reading thermal zone {Zone}", zone); }
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
