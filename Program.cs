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

// ──────────────────────────────────────────────────────────────────
// Configuration loading order:
//   1. {AppDirectory}\appsettings.json (base settings)
//   2. {AppDirectory}\appsettings.{Environment}.json (environment-specific)
//   3. C:\Users\Public\Documents\MQTT_Service\appsettings.json (legacy shared location)
//
// Environment defaults to "Production" for deployed service
// ──────────────────────────────────────────────────────────────────

var sharedConfigDir = @"C:\Users\Public\Documents\MQTT_Service";
var sharedConfigPath = Path.Combine(sharedConfigDir, "appsettings.json");

// Detect environment from DOTNET_ENVIRONMENT or ASPNETCORE_ENVIRONMENT
var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? "Production";

var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ApplicationName = "Systems One MQTT Service",
    ContentRootPath = AppContext.BaseDirectory,
    EnvironmentName = environment,
    DisableDefaults = true
});

// Load config in order of precedence
var appDirConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
var envConfigPath = Path.Combine(AppContext.BaseDirectory, $"appsettings.{environment}.json");

// 1. Base configuration
if (File.Exists(appDirConfigPath))
{
    builder.Configuration.AddJsonFile(appDirConfigPath, optional: false, reloadOnChange: true);
    Console.WriteLine($"[Config] Base: {appDirConfigPath}");
}

// 2. Environment-specific configuration (this is where installer puts settings)
if (File.Exists(envConfigPath))
{
    builder.Configuration.AddJsonFile(envConfigPath, optional: false, reloadOnChange: true);
    Console.WriteLine($"[Config] Environment ({environment}): {envConfigPath}");
}

// 3. Legacy shared location (fallback for existing installations)
if (File.Exists(sharedConfigPath))
{
    builder.Configuration.AddJsonFile(sharedConfigPath, optional: true, reloadOnChange: true);
    Console.WriteLine($"[Config] Legacy: {sharedConfigPath}");
}

if (!File.Exists(appDirConfigPath) && !File.Exists(envConfigPath) && !File.Exists(sharedConfigPath))
{
    Console.WriteLine($"[Config] WARNING: No configuration files found:");
    Console.WriteLine($"  - {appDirConfigPath}");
    Console.WriteLine($"  - {envConfigPath}");
    Console.WriteLine($"  - {sharedConfigPath}");
    Console.WriteLine($"  Service will start with default values.");
}

Console.WriteLine($"[Config] Environment: {environment}");

// Set up logging
SerilogStartup.ConfigureSerilog(builder);

if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "Systems One MQTT Service";
    });
}

// Bind options from config
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
