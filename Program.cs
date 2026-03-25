using Systems_One_MQTT_Service;
using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Collectors.OS;
using Systems_One_MQTT_Service.Collectors.App;
using Systems_One_MQTT_Service.Collectors.Database;
using Systems_One_MQTT_Service.Collectors.PLC;
using Systems_One_MQTT_Service.Collectors.Cognex;
using Systems_One_MQTT_Service.Hosting;
using Systems_One_MQTT_Service.Infrastructure;
using Systems_One_MQTT_Service.Publishing.Mqtt;
using Systems_One_MQTT_Service.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Load appsettings.Production.json if it exists (for installer-deployed credentials)
builder.Configuration.AddJsonFile("appsettings.Production.json", optional: true, reloadOnChange: true);

SerilogStartup.ConfigureSerilog(builder);

if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "Systems One MQTT Service";
    });
}

// Bind options from appsettings
builder.Services.Configure<AppCollectorOptions>(
    builder.Configuration.GetSection("AppCollector"));

builder.Services.Configure<DatabaseCollectorOptions>(
    builder.Configuration.GetSection("Database"));

builder.Services.Configure<MqttSettings>(
    builder.Configuration.GetSection("Mqtt"));

builder.Services.Configure<DiskFreeCollectorOptions>(
    builder.Configuration.GetSection("Drives"));

// Infrastructure
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IScheduler, IntervalScheduler>();

// Collectors — OS
builder.Services.AddSingleton<IMetricCollector, OsVersionCollector>();
builder.Services.AddSingleton<IMetricCollector, OsUptimeCollector>();
builder.Services.AddSingleton<IMetricCollector, DiskFreeCollector>();
builder.Services.AddSingleton<IMetricCollector, CpuUsageCollector>();
builder.Services.AddSingleton<IMetricCollector, MemoryUsageCollector>();
builder.Services.AddSingleton<IMetricCollector, TemperatureCollector>();

// Collectors — App
builder.Services.AddSingleton<IMetricCollector, AppCollector>();

// Collectors — Database
builder.Services.AddSingleton<IMetricCollector, DatabaseCollector>();

// ──────────────────────────────────────────────────────────────────
// Future collectors — uncomment when ready to enable
// ──────────────────────────────────────────────────────────────────
// builder.Services.Configure<PlcErrorCollectorOptions>(
//     builder.Configuration.GetSection("PlcErrorCollector"));
// builder.Services.AddSingleton<IMetricCollector, PlcErrorCollector>();

// builder.Services.Configure<CognexDmccOptions>(
//     builder.Configuration.GetSection("CognexDmcc"));
// builder.Services.AddSingleton<IMetricCollector, CognexDmccCollector>();
// ──────────────────────────────────────────────────────────────────

// Publishers
builder.Services.AddSingleton<MqttMetricPublisher>();
builder.Services.AddSingleton<IMetricPublisher>(sp => sp.GetRequiredService<MqttMetricPublisher>());

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<MqttHealthCheck>("mqtt")
    .AddCheck<DatabaseHealthCheck>("database");

// Hosted services
builder.Services.AddHostedService<MonitoringWorker>();
builder.Services.AddHostedService<AppRealtimeWorker>();

var host = builder.Build();
await host.RunAsync();
