using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace Systems_One_MQTT_Service.Logging;

public static class SerilogStartup
{
    public static void ConfigureSerilog(HostApplicationBuilder builder)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "system-one-.json");

        var loggerConfig = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .Enrich.WithProperty("ThreadId", Environment.CurrentManagedThreadId)
            .MinimumLevel.Warning()
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("MQTTnet", LogEventLevel.Warning)
            .MinimumLevel.Override("Systems_One_MQTT_Service.Hosting", LogEventLevel.Information)
            .MinimumLevel.Override("Systems_One_MQTT_Service.Publishing", LogEventLevel.Information)
            .WriteTo.Console()
            .WriteTo.File(new JsonFormatter(),
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                flushToDiskInterval: TimeSpan.FromSeconds(1));

        Log.Logger = loggerConfig.CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(dispose: true);
    }
}
