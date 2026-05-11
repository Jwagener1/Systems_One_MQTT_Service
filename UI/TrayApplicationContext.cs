namespace Systems_One_MQTT_Service.UI;

/// <summary>
/// Hosts a system-tray icon while the Worker Service runs.
/// Right-click menu: Settings | Restart Service | Exit
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon  _trayIcon;
    private readonly IHost       _host;
    private readonly string      _settingsPath;

    public TrayApplicationContext(IHost host, string settingsPath)
    {
        _host         = host;
        _settingsPath = settingsPath;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Icons", "systems_one.ico");
        var icon     = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;

        _trayIcon = new NotifyIcon
        {
            Icon    = icon,
            Text    = "Systems One MQTT Service",
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };

        _trayIcon.DoubleClick += (_, _) => OpenSettings();

        // Start the Worker host in the background
        Task.Run(() => _host.StartAsync());
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings…",      null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        return menu;
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settingsPath);
        form.ShowDialog();
    }

    private void ExitApplication()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Task.Run(() => _host.StopAsync()).Wait(TimeSpan.FromSeconds(5));
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
