namespace Systems_One_MQTT_Updater;

public class UpdaterSettings
{
    /// <summary>Stable URL for release-manifest.json on the latest GitHub release.</summary>
    public string ManifestUrl { get; set; } =
        "https://github.com/Jwagener1/Systems_One_MQTT_Service/releases/latest/download/release-manifest.json";

    /// <summary>Windows service name of the main service to stop/start during updates.</summary>
    public string MainServiceName { get; set; } = "Systems One MQTT Service";

    /// <summary>Install directory of the main service — binaries are swapped here.</summary>
    public string MainServiceInstallDir { get; set; } =
        @"C:\Program Files (x86)\Systems One MQTT Service";

    /// <summary>Working area for downloads, staging, and backup.</summary>
    public string UpdateCacheDir { get; set; } =
        @"C:\ProgramData\Systems One\UpdateCache";

    /// <summary>How often to poll GitHub for a new release.</summary>
    public int PollIntervalHours { get; set; } = 1;

    /// <summary>
    /// Maximum number of days an update may be deferred waiting for a quiet window
    /// before it is applied unconditionally at midnight.
    /// </summary>
    public int MaxDeferDays { get; set; } = 7;

    /// <summary>
    /// "Quiet" threshold: an update is applied only when the live 5-minute item count
    /// is at or below this percentage of the historical average for that hour.
    /// </summary>
    public double QuietThresholdPercent { get; set; } = 10.0;

    // Derived paths — not configurable
    /// <summary>
    /// When true, skips the quiet-window and activity checks and applies any staged
    /// update immediately on the next gate tick. Set to true during testing only.
    /// </summary>
    public bool BypassQuietWindow { get; set; } = false;

    // Derived paths — not configurable
    public string DownloadsDir    => Path.Combine(UpdateCacheDir, "downloads");
    public string StagingDir      => Path.Combine(UpdateCacheDir, "staging");
    public string BackupDir       => Path.Combine(UpdateCacheDir, "backup");
}
