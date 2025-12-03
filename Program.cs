using Systems_One_MQTT_Service;
using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Collectors.OS;
using Systems_One_MQTT_Service.Collectors.App;
using Systems_One_MQTT_Service.Collectors.Database;
using Systems_One_MQTT_Service.Hosting;
using Systems_One_MQTT_Service.Infrastructure;
using Systems_One_MQTT_Service.Publishing.Mqtt;
using Systems_One_MQTT_Service.Logging;

var builder = Host.CreateApplicationBuilder(args);

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

// Infrastructure
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IScheduler, IntervalScheduler>();

// Production registrations
builder.Services.AddSingleton<IMetricCollector, OsVersionCollector>();
builder.Services.AddSingleton<IMetricCollector, AppCollector>();
builder.Services.AddSingleton<IMetricCollector, DatabaseCollector>();

// Publishers (placeholder)
builder.Services.AddSingleton<IMetricPublisher, MqttMetricPublisher>();

builder.Services.AddHostedService<MonitoringWorker>();

var host = builder.Build();
await host.RunAsync();
