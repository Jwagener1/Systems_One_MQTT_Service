using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.OS;

/// <summary>
/// Collects PC temperature metrics.
/// Tries multiple methods in order of likelihood to work without elevation:
///   1. Win32_PerfFormattedData_Counters_ThermalZoneInformation (no admin)
///   2. MSAcpi_ThermalZoneTemperature (requires admin)
///   3. OpenHardwareMonitor WMI namespace (if installed)
///   4. Linux sysfs thermal zones
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
                _logger.LogDebug("No temperature sensors detected via any method");
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
            // Method 1: Formatted perf data — NO admin required, most reliable
            TryFormattedThermalZone(readings);

            // Method 2: MSAcpi — requires admin/elevated
            if (readings.Count == 0)
                TryMsAcpiThermalZone(readings);

            // Method 3: OpenHardwareMonitor / LibreHardwareMonitor WMI namespace
            if (readings.Count == 0)
                TryOpenHardwareMonitor(readings);

            // Method 4: Win32_TemperatureProbe — rarely populated
            if (readings.Count == 0)
                TryTemperatureProbe(readings);

            _logger.LogTrace("Windows temperature scan complete: {Count} sensors found", readings.Count);
        });

        return readings;
    }

    private void TryFormattedThermalZone(List<TempReading> readings)
    {
        try
        {
            _logger.LogTrace("Trying Win32_PerfFormattedData_Counters_ThermalZoneInformation (no admin)");
            using var searcher = new System.Management.ManagementObjectSearcher("root\\CIMV2",
                "SELECT Name, HighPrecisionTemperature, Temperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation");

            foreach (System.Management.ManagementObject obj in searcher.Get())
            {
                try
                {
                    // Try HighPrecisionTemperature first (10ths of Kelvin)
                    var highPrec = obj.TryGetProperty("HighPrecisionTemperature");
                    var rawTemp = obj.TryGetProperty("Temperature");
                    var name = obj["Name"]?.ToString() ?? "Thermal Zone";

                    double celsius;
                    if (highPrec != null)
                    {
                        celsius = (Convert.ToDouble(highPrec) / 10.0) - 273.15;
                        _logger.LogTrace("FormattedThermal (HighPrec): {Name} raw={Raw} → {Celsius}°C", name, highPrec, celsius);
                    }
                    else if (rawTemp != null)
                    {
                        celsius = Convert.ToDouble(rawTemp) - 273.15;
                        _logger.LogTrace("FormattedThermal (Temp): {Name} raw={Raw} → {Celsius}°C", name, rawTemp, celsius);
                    }
                    else
                    {
                        continue;
                    }

                    if (celsius is > -50 and < 150)
                    {
                        readings.Add(new TempReading { Name = name, Temperature = Math.Round(celsius, 1), Source = "PerfFormatted" });
                    }
                }
                catch (Exception ex) { _logger.LogTrace(ex, "Error reading formatted thermal entry"); }
            }

            if (readings.Count > 0)
                _logger.LogDebug("Temperature via PerfFormattedData: {Count} sensors", readings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Win32_PerfFormattedData_Counters_ThermalZoneInformation not available: {Message}", ex.Message);
        }
    }

    private void TryMsAcpiThermalZone(List<TempReading> readings)
    {
        try
        {
            _logger.LogTrace("Trying MSAcpi_ThermalZoneTemperature (requires admin)");
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
                        _logger.LogTrace("MSAcpi: {Name} = {Temp}°C", name, celsius);
                    }
                }
                catch (Exception ex) { _logger.LogTrace(ex, "Error reading MSAcpi entry"); }
            }

            if (readings.Count > 0)
                _logger.LogDebug("Temperature via MSAcpi: {Count} sensors", readings.Count);
        }
        catch (System.Management.ManagementException ex)
        {
            _logger.LogDebug("MSAcpi not available: {Message} (needs admin)", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MSAcpi query failed");
        }
    }

    private void TryOpenHardwareMonitor(List<TempReading> readings)
    {
        try
        {
            _logger.LogTrace("Trying OpenHardwareMonitor WMI namespace");
            using var searcher = new System.Management.ManagementObjectSearcher("root\\OpenHardwareMonitor",
                "SELECT Name, Value FROM Sensor WHERE SensorType='Temperature'");

            foreach (System.Management.ManagementObject obj in searcher.Get())
            {
                try
                {
                    var value = obj["Value"];
                    if (value == null) continue;
                    var celsius = Convert.ToDouble(value);
                    if (celsius is > -50 and < 150)
                    {
                        var name = obj["Name"]?.ToString() ?? "Sensor";
                        readings.Add(new TempReading { Name = name, Temperature = Math.Round(celsius, 1), Source = "OpenHardwareMonitor" });
                        _logger.LogTrace("OHM: {Name} = {Temp}°C", name, celsius);
                    }
                }
                catch (Exception ex) { _logger.LogTrace(ex, "Error reading OHM entry"); }
            }

            if (readings.Count > 0)
                _logger.LogDebug("Temperature via OpenHardwareMonitor: {Count} sensors", readings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("OpenHardwareMonitor WMI not available: {Message}", ex.Message);
        }
    }

    private void TryTemperatureProbe(List<TempReading> readings)
    {
        try
        {
            _logger.LogTrace("Trying Win32_TemperatureProbe");
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT Name, CurrentReading FROM Win32_TemperatureProbe WHERE CurrentReading IS NOT NULL");

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
                        _logger.LogTrace("Probe: {Name} = {Temp}°C", name, celsius);
                    }
                }
                catch (Exception ex) { _logger.LogTrace(ex, "Error reading temperature probe"); }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Win32_TemperatureProbe not available: {Message}", ex.Message);
        }
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

// Extension to safely get WMI properties that may not exist
internal static class ManagementObjectExtensions
{
    public static object? TryGetProperty(this System.Management.ManagementObject obj, string propertyName)
    {
        try
        {
            return obj[propertyName];
        }
        catch
        {
            return null;
        }
    }
}
