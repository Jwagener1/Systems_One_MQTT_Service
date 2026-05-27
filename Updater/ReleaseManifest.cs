namespace Systems_One_MQTT_Updater;

public class ReleaseManifest
{
    public string Version              { get; set; } = string.Empty;
    public string PublishedAt          { get; set; } = string.Empty;
    public string DownloadUrl          { get; set; } = string.Empty;
    public string Sha256               { get; set; } = string.Empty;
    public string MinimumUpdaterVersion { get; set; } = "1.0.0";
}
