using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace Systems_One_MQTT_Service.Logging;

public static class SerilogStartup
{
    public static void ConfigureSerilog(HostApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var environment = builder.Environment.EnvironmentName ?? "Production";
        var isDevelopment = string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);

        var loggerConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .Enrich.WithProperty("ThreadId", Environment.CurrentManagedThreadId)
            .Enrich.WithProperty("Environment", environment)
            .MinimumLevel.Is(isDevelopment ? LogEventLevel.Debug : LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.Hosting", LogEventLevel.Information)
            .MinimumLevel.Override("MQTTnet", LogEventLevel.Warning)
            .WriteTo.Console()
            .WriteTo.File(new JsonFormatter(),
                path: "logs/system-one-.json",
                rollingInterval: RollingInterval.Day,
                restrictedToMinimumLevel: isDevelopment ? LogEventLevel.Debug : LogEventLevel.Information,
                flushToDiskInterval: TimeSpan.FromSeconds(1));

        Log.Logger = loggerConfig.CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(dispose: true);
    }
}
