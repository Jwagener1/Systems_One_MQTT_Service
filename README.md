# Systems One MQTT Service

A .NET 8 Worker Service that collects metrics from OS, App, and Database, and logs/publishes them.

## Project overview
- Collectors implement `IMetricCollector` and return `Metric` objects.
- `Worker` runs collectors on an interval and logs metrics.
- Configuration comes from `appsettings.json`.

## Current collectors
- `Collectors/OS/OsVersionCollector`: OS version/description.
- `Collectors/App/AppCollector`: App running state and JSON settings from a directory.
- `Collectors/Database/DatabaseCollector`: SQL Server connectivity and `ItemLog` 5-minute window summary + sample.

## Configuration
Update `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
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
    "Username": "SysOne",
    "Password": "SysOne012!"
  },
  "Drives": {
    "Monitors": [
      { "Path": "C:" },
      { "Path": "D:" }
    ]
  }
}
```

### App collector
- Reads process name from `AppCollector:ExePath` (filename without `.exe`).
- Detects running by process name; attempts exact path match when accessible.
- Enumerates all `*.json` files in `AppCollector:SettingsDir` and emits metrics keyed by filename.
- Metric examples:
  - `app.running` with tags `process_name`, `exe_path`, `process_count`, `path_match`.
  - `app.settings.<filename>` with tag `path`.

### Database collector
- Uses `Microsoft.Data.SqlClient`.
- Binds `Database` section and builds a connection string.
- Computes window: last complete 5-minute period ending 5 minutes before now.
- Emits:
  - `db.connection.status` (true/false).
  - `db.itemlog.window.summary` with totals:
    - `Total_Items`, `No_Read`, `Good_Read`, `No_Dimension`, `No_Weight`, `Data_Sent`, `Not_Sent`, `Image_Sent`, `Image_Not_Sent`, `Item_Out_Of_Spec`, `More_Than_1_Item`.
  - `db.itemlog.window.count` (row count).
  - `db.itemlog.window.sample` (first row).

## DI registrations
In `Program.cs`:
- Configure options: `AppCollectorOptions` from `AppCollector`, `DatabaseCollectorOptions` from `Database`.
- Register collectors:
  - `IMetricCollector` ? `OsVersionCollector`, `AppCollector`, `DatabaseCollector`.
- Add hosted service: `Worker`.

## Proposed folder structure
Inside `Systems_One_MQTT_Service`:
- `Abstractions`
- `Metrics`
- `Collectors`
  - `OS`
  - `App`
  - `DB`
  - `Infra`
- `Publishing/Mqtt`
- `Configuration`
- `Infrastructure`
- `Hosting`

Rationale: feature-oriented structure (metrics ? collectors ? publishing) with a thin hosting layer. Interfaces and models are separate for testability and maintainability.

## Build & run
- Requires .NET 8 SDK.
- Packages: `Microsoft.Data.SqlClient`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.WindowsServices`.
- Build: `dotnet build`
- Run: `dotnet run`
- As Windows Service: windows-only; install with `sc` or PowerShell service tools.

## Extending
- Add a new collector class implementing `IMetricCollector` under the appropriate folder.
- Bind options from `appsettings.json` and register in `Program.cs`.
- Keep collectors focused on one concern and return `Metric` objects only.