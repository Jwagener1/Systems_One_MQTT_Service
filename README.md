# Systems One MQTT Service

A .NET 8 Worker Service that collects system, application, and database metrics and publishes them to an MQTT broker.

## Architecture Overview

The service follows a **collector → publisher** pattern with two background workers:

```
┌─────────────────────────────────────────────────────────────────┐
│                        Hosted Services                          │
├────────────────────────────┬────────────────────────────────────┤
│    MonitoringWorker        │       AppRealtimeWorker            │
│  (5-minute interval)       │     (2-second interval)            │
│  - All collectors          │     - App collector only           │
└────────────────────────────┴────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      IMetricCollector                           │
├─────────────┬─────────────┬─────────────┬─────────────┬─────────┤
│ OsVersion   │ OsUptime    │ DiskFree    │ CpuUsage    │ Memory  │
│ Collector   │ Collector   │ Collector   │ Collector   │ Usage   │
├─────────────┼─────────────┴─────────────┴─────────────┴─────────┤
│ AppCollector│              DatabaseCollector                    │
└─────────────┴───────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     IMetricPublisher                            │
│                    (MqttMetricPublisher)                        │
│   - Connects to MQTT broker with exponential backoff retry      │
│   - Publishes status (online/offline) via LWT                   │
│   - Topic format: {BaseTopic}/{MachineName}/{Source}/{MetricId} │
└─────────────────────────────────────────────────────────────────┘
```

## Core Components

### Abstractions

| Interface | Description |
|-----------|-------------|
| `IMetricCollector` | Contract for collecting metrics. Returns `IEnumerable<Metric>`. |
| `IMetricPublisher` | Contract for publishing metrics. Supports connect/disconnect lifecycle. |
| `IScheduler` | Abstraction for scheduled execution (used by `MonitoringWorker`). |
| `IClock` | Time abstraction for testability. |

### Metric Model

```csharp
public class Metric
{
    public string Id { get; set; }        // e.g., "os.version", "cpu.usage"
    public string Name { get; set; }      // Human-readable name
    public object Value { get; set; }     // Metric value (can be primitive or complex object)
    public string? Unit { get; set; }     // e.g., "percent", "MB", "seconds"
    public DateTimeOffset Timestamp { get; set; }
    public string Source { get; set; }    // e.g., "OS", "App", "DB"
    public Dictionary<string, object>? Tags { get; set; }  // Additional metadata
}
```

## Hosted Services

### MonitoringWorker
- **Interval**: Configurable via `Monitoring:IntervalMinutes` (default: 5 minutes)
- **Behavior**: Runs all registered `IMetricCollector` instances sequentially
- **Lifecycle**: Connects to MQTT on start, disconnects on stop

### AppRealtimeWorker
- **Interval**: Fixed 2-second loop
- **Behavior**: Runs only collectors named "App" for near real-time monitoring
- **Purpose**: Detects app running state changes and settings file modifications quickly

## Collectors

### OS Collectors

| Collector | Metric ID(s) | Description |
|-----------|--------------|-------------|
| `OsVersionCollector` | `os.version` | OS version string with platform/version tags |
| `OsUptimeCollector` | `os.uptime` | System uptime in seconds with day/hour/minute breakdown |
| `CpuUsageCollector` | `cpu.usage` | CPU usage percentage (uses Windows Performance Counter or process-level fallback) |
| `MemoryUsageCollector` | `memory.total`, `memory.available`, `memory.used`, `memory.usage` | Memory metrics in MB and percentage |
| `DiskFreeCollector` | `os.drives` | Disk space for configured drives (total/free/used GB, usage %) |

### App Collector

Monitors application state and configuration files:

- **`app.running`**: Emitted when running state changes
  - Tags: `process_name`, `exe_path`, `process_count`, `path_match`
- **`app.settings.<filename>`**: Emitted when a JSON settings file changes (hash-based change detection)
  - Tags: `path`, `hash`

### Database Collector

Connects to SQL Server and queries the `ItemLog` table:

- **`db.connection`**: Connection status (true/false)
  - Tags: `server`, `database`, `table`
- **`db.itemlog.summary`**: 5-minute window aggregation
  - Values: `Total_Items`, `No_Read`, `No_Dimension`, `No_Weight`, `Data_Sent`, `Image_Sent`, `Item_Out_Of_Spec`, `More_Than_1_Item`
  - Tags: `startUtc`, `endUtc`
- **`db.query.error`**: Emitted on query failure with error message

## MQTT Publishing

### Connection
- Exponential backoff retry (up to 5 attempts, 1-30 second delays with jitter)
- Last Will and Testament (LWT) for offline detection

### Topic Structure
```
{BaseTopic}/{MachineName}/{Source}/{MetricPath}
```
Example: `systems-one/WORKSTATION01/OS/cpu/usage`

### Status Topic
```
{BaseTopic}/{MachineName}/status  →  "online" | "offline"
```

## Configuration

All configuration is in `appsettings.json` (or `appsettings.Production.json` for deployed credentials):

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    }
  },
  "AppCollector": {
    "ExePath": "C:\\Program Files\\SystemsOne\\StaticInstaller\\Sys_One_Static_App.exe",
    "SettingsDir": "C:\\Users\\Public\\Documents\\SystemOne_App_Settings"
  },
  "Database": {
    "Server": "192.168.1.16,1433",
    "DatabaseName": "Systems_One",
    "TableName": "ItemLog",
    "Username": "",
    "Password": "",
    "TimeoutSeconds": 5
  },
  "Drives": {
    "Monitors": [
      { "Path": "C:" },
      { "Path": "D:" }
    ]
  },
  "Monitoring": {
    "IntervalMinutes": 5
  },
  "Mqtt": {
    "BrokerUrl": "mqtt://192.168.1.16",
    "BrokerPort": 1883,
    "ClientId": "systems-one-service",
    "Username": "",
    "Password": "",
    "BaseTopic": "systems-one"
  }
}
```

### Configuration Sections

| Section | Purpose |
|---------|---------|
| `Serilog` | Logging configuration (console + rolling JSON file) |
| `AppCollector` | Path to monitored executable and settings directory |
| `Database` | SQL Server connection details |
| `Drives:Monitors` | List of drive paths to monitor |
| `Monitoring` | Polling interval for MonitoringWorker |
| `Mqtt` | MQTT broker connection settings |

## Logging

Uses **Serilog** with:
- Console output
- Rolling JSON file logs at `logs/system-one-{date}.json`
- Enrichment: `MachineName`, `ProcessId`, `ThreadId`, `Environment`
- Structured logging with scopes for component tracking

## Project Structure

```
Systems_One_MQTT_Service/
├── Abstractions/           # Interfaces (IMetricCollector, IMetricPublisher, etc.)
├── Collectors/
│   ├── App/                # AppCollector, AppCollectorOptions
│   ├── Database/           # DatabaseCollector, ItemLogQuery, options
│   └── OS/                 # OS metric collectors
├── Hosting/                # BackgroundService workers
├── Infrastructure/         # SystemClock, IntervalScheduler
├── Logging/                # Serilog configuration
├── Metrics/                # Metric model
├── Publishing/
│   └── Mqtt/               # MQTT publisher, settings, topic builder
├── Program.cs              # DI registration and host setup
└── appsettings.json        # Configuration
```

## Build & Run

### Prerequisites
- .NET 8 SDK
- (Optional) SQL Server for database metrics
- (Optional) MQTT broker (e.g., Mosquitto)

### Commands
```bash
# Build
dotnet build

# Run
dotnet run

# Publish for deployment
dotnet publish -c Release
```

### Windows Service Installation
```powershell
sc create "Systems One MQTT Service" binPath="C:\path\to\Systems_One_MQTT_Service.exe"
sc start "Systems One MQTT Service"
```

## Extending the Service

### Adding a New Collector

1. Create a class implementing `IMetricCollector`:
```csharp
public class MyCollector : IMetricCollector
{
    public string Name => "MySource";

    public Task<IEnumerable<Metric>> CollectAsync(CancellationToken ct = default)
    {
        // Return metrics
    }
}
```

2. Register in `Program.cs`:
```csharp
builder.Services.AddSingleton<IMetricCollector, MyCollector>();
```

3. (Optional) Add configuration options and bind from `appsettings.json`.

### Adding a New Publisher

1. Implement `IMetricPublisher`
2. Replace or chain with existing publisher registration in `Program.cs`