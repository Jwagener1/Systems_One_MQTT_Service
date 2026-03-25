using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.OS;

/// <summary>
/// Collects system temperature from ACPI thermal zones.
/// Reports motherboard/chipset temperature (not CPU package — that requires admin or third-party tools).
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
                tempCelsius = await GetWindowsTempAsync();
            else if (OperatingSystem.IsLinux())
                tempCelsius = await GetLinuxTempAsync();

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
                    Value = new { celsius = tempCelsius.Value, status },
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

    private async Task<double?> GetWindowsTempAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher("root\\CIMV2",
                    "SELECT Temperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation");

                double? maxTemp = null;

                foreach (System.Management.ManagementObject obj in searcher.Get())
                {
                    var raw = obj["Temperature"];
                    if (raw == null) continue;

                    var celsius = Convert.ToDouble(raw) - 273.15;
                    _logger.LogTrace("Thermal zone: {Temp}°C", Math.Round(celsius, 1));

                    if (celsius is > -50 and < 150)
                        maxTemp = Math.Max(maxTemp ?? 0, Math.Round(celsius, 1));
                }

                return maxTemp;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Thermal zone query failed: {Message}", ex.Message);
                return null;
            }
        });
    }

    private async Task<double?> GetLinuxTempAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists("/sys/class/thermal")) return null;

                double? maxTemp = null;

                foreach (var zone in Directory.GetDirectories("/sys/class/thermal", "thermal_zone*"))
                {
                    var tempFile = Path.Combine(zone, "temp");
                    if (!File.Exists(tempFile)) continue;

                    if (double.TryParse(File.ReadAllText(tempFile).Trim(), out var milliCelsius))
                    {
                        var celsius = milliCelsius / 1000.0;
                        if (celsius is > -50 and < 150)
                            maxTemp = Math.Max(maxTemp ?? 0, Math.Round(celsius, 1));
                    }
                }

                return maxTemp;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Linux thermal read failed");
                return null;
            }
        });
    }
}
