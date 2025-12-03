using Systems_One_MQTT_Service;
using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Collectors.OS;
using Systems_One_MQTT_Service.Collectors.App;
using Systems_One_MQTT_Service.Collectors.Database;

var builder = Host.CreateApplicationBuilder(args);

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

// Production registrations
builder.Services.AddSingleton<IMetricCollector, OsVersionCollector>();
builder.Services.AddSingleton<IMetricCollector, AppCollector>();
builder.Services.AddSingleton<IMetricCollector, DatabaseCollector>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
