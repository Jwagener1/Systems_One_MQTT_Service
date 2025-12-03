using Systems_One_MQTT_Service;
using Systems_One_MQTT_Service.Abstractions;
using Systems_One_MQTT_Service.Collectors.OS;

var builder = Host.CreateApplicationBuilder(args);

if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "Systems One MQTT Service";
    });
}

// Production registrations
builder.Services.AddSingleton<IMetricCollector, OsVersionCollector>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
