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

        // Logging levels:
        //   Development: Debug (see all collector runs, SQL queries, MQTT publishes)
        //   Production:  Warning (only warnings, errors, and critical — plus startup/shutdown Info)
        //
        // To get Trace-level output, set Serilog:MinimumLevel:Default to "Verbose" in appsettings.

        var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "system-one-.json");

        var loggerConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("MachineName", Environment.MachineName)
            .Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .Enrich.WithProperty("ThreadId", Environment.CurrentManagedThreadId)
            .Enrich.WithProperty("Environment", environment)
            .MinimumLevel.Is(isDevelopment ? LogEventLevel.Debug : LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft", isDevelopment ? LogEventLevel.Information : LogEventLevel.Warning)
            .MinimumLevel.Override("System", isDevelopment ? LogEventLevel.Information : LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information) // Always log startup/shutdown
            .MinimumLevel.Override("MQTTnet", LogEventLevel.Warning)
            // Allow our own namespace to log at Info even in production for key lifecycle events
            .MinimumLevel.Override("Systems_One_MQTT_Service.Hosting", isDevelopment ? LogEventLevel.Debug : LogEventLevel.Information)
            .MinimumLevel.Override("Systems_One_MQTT_Service.Publishing", isDevelopment ? LogEventLevel.Debug : LogEventLevel.Information)
            .WriteTo.Console()
            .WriteTo.File(new JsonFormatter(),
                path: logPath,
                rollingInterval: RollingInterval.Day,
                restrictedToMinimumLevel: isDevelopment ? LogEventLevel.Debug : LogEventLevel.Warning,
                retainedFileCountLimit: 30,
                flushToDiskInterval: TimeSpan.FromSeconds(1));

        Log.Logger = loggerConfig.CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(dispose: true);
    }
}
