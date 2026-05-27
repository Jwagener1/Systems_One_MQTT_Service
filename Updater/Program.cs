using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Systems_One_MQTT_Updater;
using Systems_One_MQTT_Updater.GitHub;
using Systems_One_MQTT_Updater.Hosting;
using Systems_One_MQTT_Updater.Reporting;
using Systems_One_MQTT_Updater.Scheduling;
using Systems_One_MQTT_Updater.Update;

var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ApplicationName = "Systems One MQTT Updater",
    ContentRootPath = AppContext.BaseDirectory,
    DisableDefaults = true
});

// Updater's own config
var updaterConfigPath = Path.Combine(AppContext.BaseDirectory, "updater-settings.json");
builder.Configuration.AddJsonFile(updaterConfigPath, optional: false, reloadOnChange: true);

// Main service's appsettings.json — provides MQTT + DB settings
builder.Services.Configure<UpdaterSettings>(builder.Configuration.GetSection("Updater"));

var mainServiceDir = builder.Configuration["Updater:MainServiceInstallDir"]
    ?? @"C:\Program Files\Systems One MQTT Service";
var mainAppSettings = Path.Combine(mainServiceDir, "appsettings.json");
builder.Configuration.AddJsonFile(mainAppSettings, optional: true, reloadOnChange: false);

// Serilog
var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        new Serilog.Formatting.Compact.CompactJsonFormatter(),
        Path.Combine(logDir, "updater-.json"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

builder.Services.AddSerilog();

if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "Systems One MQTT Updater";
    });
}

// Core services
builder.Services.AddHttpClient<ReleaseChecker>();
builder.Services.AddSingleton<ReleaseChecker>();
builder.Services.AddSingleton<UpdateDownloader>();
builder.Services.AddSingleton<UpdateApplicator>();
builder.Services.AddSingleton<QuietWindowAnalyzer>();
builder.Services.AddSingleton<ActivityChecker>();
builder.Services.AddSingleton<UpdaterMqttReporter>();
builder.Services.AddHostedService<UpdaterWorker>();

var host = builder.Build();
await host.RunAsync();
