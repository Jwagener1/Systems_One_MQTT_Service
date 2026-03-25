using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.OS;

/// <summary>
/// Collects system temperature from Windows thermal zone performance counters.
/// No admin privileges required.
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

            if (OperatingSystem.IsWindows())
            {
                tempCelsius = await GetWindowsTemperatureAsync();
            }
            else if (OperatingSystem.IsLinux())
            {
                tempCelsius = await GetLinuxTemperatureAsync();
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
                    Value = new
                    {
                        celsius = tempCelsius.Value,
                        status
                    },
                    Unit = "°C",
                    Source = "OS",
                    Timestamp = _clock.UtcNow
                });

                _logger.LogDebug("Temperature: {Temp}°C ({Status})", tempCelsius.Value, status);
            }
            else
            {
                metrics.Add(new Metric
                {
                    Id = "temperature",
                    Name = "System Temperature",
                    Value = new { celsius = (double?)null, status = "unavailable" },
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

    private async Task<double?> GetWindowsTemperatureAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger.LogTrace("Querying Win32_PerfFormattedData_Counters_ThermalZoneInformation");

                using var searcher = new System.Management.ManagementObjectSearcher("root\\CIMV2",
                    "SELECT Temperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation");

                var temps = new List<double>();

                foreach (System.Management.ManagementObject obj in searcher.Get())
                {
                    try
                    {
                        var raw = obj["Temperature"];
                        if (raw == null) continue;

                        // Value is in Kelvin
                        var kelvin = Convert.ToDouble(raw);
                        var celsius = kelvin - 273.15;

                        _logger.LogTrace("Thermal zone: raw={Raw}K → {Celsius}°C", kelvin, Math.Round(celsius, 1));

                        if (celsius is > -50 and < 150)
                            temps.Add(Math.Round(celsius, 1));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace(ex, "Error reading thermal zone entry");
                    }
                }

                if (temps.Count > 0)
                {
                    var max = temps.Max();
                    _logger.LogDebug("Read {Count} thermal zones, max={Max}°C", temps.Count, max);
                    return (double?)max;
                }

                _logger.LogDebug("No thermal zone data returned");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("ThermalZoneInformation query failed: {Message}", ex.Message);
                return null;
            }
        });
    }

    private async Task<double?> GetLinuxTemperatureAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists("/sys/class/thermal")) return null;

                var temps = new List<double>();

                foreach (var zone in Directory.GetDirectories("/sys/class/thermal", "thermal_zone*"))
                {
                    var tempFile = Path.Combine(zone, "temp");
                    if (!File.Exists(tempFile)) continue;

                    var text = File.ReadAllText(tempFile).Trim();
                    if (double.TryParse(text, out var milliCelsius))
                    {
                        var celsius = milliCelsius / 1000.0;
                        if (celsius is > -50 and < 150)
                        {
                            temps.Add(Math.Round(celsius, 1));
                            _logger.LogTrace("Linux thermal zone: {Temp}°C", celsius);
                        }
                    }
                }

                return temps.Count > 0 ? (double?)temps.Max() : null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Linux thermal zone read failed");
                return null;
            }
        });
    }
}
