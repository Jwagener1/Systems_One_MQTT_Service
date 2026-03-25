using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Metrics;

namespace Systems_One_MQTT_Service.Collectors.OS;

/// <summary>
/// Collects CPU temperature via MSAcpi_ThermalZoneTemperature (requires admin).
/// Falls back to ACPI perf counters if WMI root\WMI is unavailable.
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
                tempCelsius = await GetMsAcpiTempAsync();

                if (!tempCelsius.HasValue)
                    tempCelsius = await GetPerfCounterTempAsync();
            }
            else if (OperatingSystem.IsLinux())
            {
                tempCelsius = await GetLinuxTempAsync();
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

    /// <summary>
    /// MSAcpi_ThermalZoneTemperature — requires admin/LocalSystem.
    /// Returns actual CPU thermal zone temperature.
    /// </summary>
    private async Task<double?> GetMsAcpiTempAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger.LogTrace("Querying MSAcpi_ThermalZoneTemperature (root\\WMI)");

                using var searcher = new System.Management.ManagementObjectSearcher("root\\WMI",
                    "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

                double? maxTemp = null;

                foreach (System.Management.ManagementObject obj in searcher.Get())
                {
                    var raw = obj["CurrentTemperature"];
                    if (raw == null) continue;

                    // Value is in tenths of Kelvin
                    var celsius = (Convert.ToDouble(raw) / 10.0) - 273.15;
                    _logger.LogTrace("MSAcpi zone: {Temp}°C", Math.Round(celsius, 1));

                    if (celsius is > -50 and < 150)
                        maxTemp = Math.Max(maxTemp ?? 0, Math.Round(celsius, 1));
                }

                if (maxTemp.HasValue)
                    _logger.LogDebug("MSAcpi temperature: {Temp}°C", maxTemp.Value);

                return maxTemp;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("MSAcpi query failed: {Message}", ex.Message);
                return null;
            }
        });
    }

    /// <summary>
    /// Fallback: ACPI perf counters (no admin, but reports motherboard/chipset temps).
    /// </summary>
    private async Task<double?> GetPerfCounterTempAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger.LogTrace("Falling back to ThermalZoneInformation perf counters");

                using var searcher = new System.Management.ManagementObjectSearcher("root\\CIMV2",
                    "SELECT Temperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation");

                double? maxTemp = null;

                foreach (System.Management.ManagementObject obj in searcher.Get())
                {
                    var raw = obj["Temperature"];
                    if (raw == null) continue;

                    var celsius = Convert.ToDouble(raw) - 273.15;
                    if (celsius is > -50 and < 150)
                        maxTemp = Math.Max(maxTemp ?? 0, Math.Round(celsius, 1));
                }

                return maxTemp;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("PerfCounter thermal query failed: {Message}", ex.Message);
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
