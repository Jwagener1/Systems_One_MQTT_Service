using Systems_One_MQTT_Service;

var builder = Host.CreateApplicationBuilder(args);

if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "Systems One MQTT Service";
    });
}

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
