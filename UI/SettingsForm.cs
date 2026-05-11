using System.Text.Json;
using System.Text.Json.Nodes;

namespace Systems_One_MQTT_Service.UI;

public sealed class SettingsForm : Form
{
    // ── MQTT controls ────────────────────────────────────────────────────────
    private readonly TextBox _mqttBrokerUrl     = new() { Width = 300 };
    private readonly NumericUpDown _mqttPort    = new() { Minimum = 0, Maximum = 65535, Width = 100 };
    private readonly TextBox _mqttUsername      = new() { Width = 200 };
    private readonly TextBox _mqttPassword      = new() { Width = 200, UseSystemPasswordChar = true };
    private readonly TextBox _mqttBaseTopic     = new() { Width = 200 };
    private readonly TextBox _mqttCompany       = new() { Width = 200 };
    private readonly TextBox _mqttLocation      = new() { Width = 200 };
    private readonly TextBox _mqttMachineId     = new() { Width = 200 };
    private readonly TextBox _mqttSerialNumber  = new() { Width = 200 };
    private readonly CheckBox _mqttTls          = new() { Text = "Encryption TLS" };
    private readonly CheckBox _mqttValidateCert = new() { Text = "Validate Certificate" };

    // ── Database controls ────────────────────────────────────────────────────
    private readonly TextBox _dbServer      = new() { Width = 300 };
    private readonly TextBox _dbName        = new() { Width = 300 };
    private readonly TextBox _dbTableName   = new() { Width = 200, PlaceholderText = "Leave blank for schema default" };
    private readonly TextBox _dbUsername    = new() { Width = 200 };
    private readonly TextBox _dbPassword    = new() { Width = 200, UseSystemPasswordChar = true };
    private readonly NumericUpDown _dbTimeout = new() { Minimum = 1, Maximum = 300, Width = 80 };
    private readonly ComboBox _dbSchema     = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };

    // ── App Watcher controls ─────────────────────────────────────────────────
    private readonly TextBox _appExePath    = new() { Width = 350 };
    private readonly Button  _appExeBrowse  = new() { Text = "Browse…", Width = 80 };
    private readonly TextBox _appSettingsDir = new() { Width = 350 };
    private readonly Button  _appDirBrowse  = new() { Text = "Browse…", Width = 80 };

    private readonly string _settingsPath;

    public SettingsForm(string settingsPath)
    {
        _settingsPath = settingsPath;

        Text            = "Systems One MQTT Service — Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        AutoSize        = true;
        AutoSizeMode    = AutoSizeMode.GrowAndShrink;
        Padding         = new Padding(10);

        var tabs = new TabControl { Dock = DockStyle.Fill, Width = 520, Height = 460 };
        tabs.TabPages.Add(BuildMqttTab());
        tabs.TabPages.Add(BuildDatabaseTab());
        tabs.TabPages.Add(BuildAppWatcherTab());

        var btnSave   = new Button { Text = "Save",   DialogResult = DialogResult.OK,     Width = 80 };
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
        btnSave.Click += (_, _) => SaveSettings();

        var buttonRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock          = DockStyle.Bottom,
            AutoSize      = true,
            Padding       = new Padding(0, 6, 0, 0)
        };
        buttonRow.Controls.Add(btnCancel);
        buttonRow.Controls.Add(btnSave);

        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, AutoSize = true };
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        outer.Controls.Add(tabs,       0, 0);
        outer.Controls.Add(buttonRow,  0, 1);
        Controls.Add(outer);

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        // Populate dropdown values
        _dbSchema.Items.AddRange(new object[] { "Default", "Snowsoft", "Madibana" });

        LoadSettings();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tab builders
    // ─────────────────────────────────────────────────────────────────────────

    private TabPage BuildMqttTab()
    {
        var tab = new TabPage("MQTT");
        var layout = BuildGrid(tab);
        int row = 0;
        AddRow(layout, row++, "Broker URL:",     _mqttBrokerUrl);
        AddRow(layout, row++, "Broker Port:",    _mqttPort);
        AddRow(layout, row++, "Username:",       _mqttUsername);
        AddRow(layout, row++, "Password:",       _mqttPassword);
        AddRow(layout, row++, "Base Topic:",     _mqttBaseTopic);
        AddRow(layout, row++, "Company:",        _mqttCompany);
        AddRow(layout, row++, "Location:",       _mqttLocation);
        AddRow(layout, row++, "Machine ID:",     _mqttMachineId);
        AddRow(layout, row++, "Serial Number:",  _mqttSerialNumber);
        AddRow(layout, row++, "",                _mqttTls);
        AddRow(layout, row++, "",                _mqttValidateCert);
        return tab;
    }

    private TabPage BuildDatabaseTab()
    {
        var tab = new TabPage("Database");
        var layout = BuildGrid(tab);
        int row = 0;

        var schemaLabel = new Label
        {
            Text      = "⚙ Choosing a schema sets the default table name and query logic for that customer.",
            AutoSize  = true,
            ForeColor = SystemColors.GrayText,
            Padding   = new Padding(0, 0, 0, 4)
        };
        layout.SetColumnSpan(schemaLabel, 2);
        layout.Controls.Add(schemaLabel, 0, row++);

        AddRow(layout, row++, "Customer Schema:", _dbSchema);
        AddRow(layout, row++, "Server:",          _dbServer);
        AddRow(layout, row++, "Database Name:",   _dbName);
        AddRow(layout, row++, "Table Name:",      _dbTableName);
        AddRow(layout, row++, "Username:",        _dbUsername);
        AddRow(layout, row++, "Password:",        _dbPassword);
        AddRow(layout, row++, "Timeout (sec):",   _dbTimeout);

        // Schema hint label — updates when the dropdown changes
        var hintLabel = new Label { AutoSize = true, ForeColor = SystemColors.GrayText };
        UpdateSchemaHint(hintLabel);
        _dbSchema.SelectedIndexChanged += (_, _) => UpdateSchemaHint(hintLabel);
        layout.SetColumnSpan(hintLabel, 2);
        layout.Controls.Add(hintLabel, 0, row);

        return tab;
    }

    private TabPage BuildAppWatcherTab()
    {
        var tab = new TabPage("App Watcher");
        var layout = BuildGrid(tab);
        int row = 0;

        // ExePath row with inline browse button
        var exePanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        exePanel.Controls.Add(_appExePath);
        exePanel.Controls.Add(_appExeBrowse);
        _appExeBrowse.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog
            {
                Title       = "Select Application Executable",
                Filter      = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                FilterIndex = 1
            };
            if (!string.IsNullOrWhiteSpace(_appExePath.Text) && File.Exists(_appExePath.Text))
                dlg.InitialDirectory = Path.GetDirectoryName(_appExePath.Text);
            if (dlg.ShowDialog() == DialogResult.OK)
                _appExePath.Text = dlg.FileName;
        };

        // SettingsDir row with inline browse button
        var dirPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        dirPanel.Controls.Add(_appSettingsDir);
        dirPanel.Controls.Add(_appDirBrowse);
        _appDirBrowse.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog
            {
                Description         = "Select Application Settings Directory",
                UseDescriptionForTitle = true
            };
            if (!string.IsNullOrWhiteSpace(_appSettingsDir.Text) && Directory.Exists(_appSettingsDir.Text))
                dlg.InitialDirectory = _appSettingsDir.Text;
            if (dlg.ShowDialog() == DialogResult.OK)
                _appSettingsDir.Text = dlg.SelectedPath;
        };

        AddRow(layout, row++, "Executable (.exe):", exePanel);
        AddRow(layout, row,   "Settings Directory:", dirPanel);
        return tab;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Load / Save
    // ─────────────────────────────────────────────────────────────────────────

    private void LoadSettings()
    {
        if (!File.Exists(_settingsPath)) return;

        try
        {
            var json = JsonNode.Parse(File.ReadAllText(_settingsPath))?.AsObject();
            if (json == null) return;

            // MQTT
            var mqtt = json["Mqtt"]?.AsObject();
            if (mqtt != null)
            {
                _mqttBrokerUrl.Text    = mqtt["BrokerUrl"]?.GetValue<string>()    ?? string.Empty;
                _mqttPort.Value        = mqtt["BrokerPort"]?.GetValue<int>()      ?? 1883;
                _mqttUsername.Text     = mqtt["Username"]?.GetValue<string>()     ?? string.Empty;
                _mqttPassword.Text     = mqtt["Password"]?.GetValue<string>()     ?? string.Empty;
                _mqttBaseTopic.Text    = mqtt["BaseTopic"]?.GetValue<string>()    ?? string.Empty;
                _mqttCompany.Text      = mqtt["Company"]?.GetValue<string>()      ?? string.Empty;
                _mqttLocation.Text     = mqtt["Location"]?.GetValue<string>()     ?? string.Empty;
                _mqttMachineId.Text    = mqtt["MachineId"]?.GetValue<string>()    ?? string.Empty;
                _mqttSerialNumber.Text = mqtt["SerialNumber"]?.GetValue<string>() ?? string.Empty;
                _mqttTls.Checked          = mqtt["EncryptionTLS"]?.GetValue<bool>()       ?? false;
                _mqttValidateCert.Checked = mqtt["ValidateCertificate"]?.GetValue<bool>() ?? true;
            }

            // Database
            var db = json["Database"]?.AsObject();
            if (db != null)
            {
                _dbServer.Text    = db["Server"]?.GetValue<string>()       ?? string.Empty;
                _dbName.Text      = db["DatabaseName"]?.GetValue<string>() ?? string.Empty;
                _dbTableName.Text = db["TableName"]?.GetValue<string>()    ?? string.Empty;
                _dbUsername.Text  = db["Username"]?.GetValue<string>()     ?? string.Empty;
                _dbPassword.Text  = db["Password"]?.GetValue<string>()     ?? string.Empty;
                _dbTimeout.Value  = db["TimeoutSeconds"]?.GetValue<int>()  ?? 30;

                var schema = db["SchemaType"]?.GetValue<string>() ?? "Default";
                _dbSchema.SelectedItem = _dbSchema.Items.Contains(schema) ? schema : "Default";
            }

            // App Watcher
            var app = json["AppCollector"]?.AsObject();
            if (app != null)
            {
                _appExePath.Text     = app["ExePath"]?.GetValue<string>()     ?? string.Empty;
                _appSettingsDir.Text = app["SettingsDir"]?.GetValue<string>() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load settings:\n{ex.Message}", "Settings Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void SaveSettings()
    {
        try
        {
            JsonObject json;
            if (File.Exists(_settingsPath))
                json = JsonNode.Parse(File.ReadAllText(_settingsPath))?.AsObject() ?? new JsonObject();
            else
                json = new JsonObject();

            json["Mqtt"] = new JsonObject
            {
                ["BrokerUrl"]          = _mqttBrokerUrl.Text,
                ["BrokerPort"]         = (int)_mqttPort.Value,
                ["Username"]           = _mqttUsername.Text,
                ["Password"]           = _mqttPassword.Text,
                ["BaseTopic"]          = _mqttBaseTopic.Text,
                ["Company"]            = _mqttCompany.Text,
                ["Location"]           = _mqttLocation.Text,
                ["MachineId"]          = _mqttMachineId.Text,
                ["SerialNumber"]       = _mqttSerialNumber.Text,
                ["EncryptionTLS"]      = _mqttTls.Checked,
                ["ValidateCertificate"] = _mqttValidateCert.Checked
            };

            json["Database"] = new JsonObject
            {
                ["Server"]        = _dbServer.Text,
                ["DatabaseName"]  = _dbName.Text,
                ["TableName"]     = _dbTableName.Text,
                ["Username"]      = _dbUsername.Text,
                ["Password"]      = _dbPassword.Text,
                ["TimeoutSeconds"] = (int)_dbTimeout.Value,
                ["SchemaType"]    = _dbSchema.SelectedItem?.ToString() ?? "Default"
            };

            json["AppCollector"] = new JsonObject
            {
                ["ExePath"]    = _appExePath.Text,
                ["SettingsDir"] = _appSettingsDir.Text
            };

            var opts = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_settingsPath, json.ToJsonString(opts));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings:\n{ex.Message}", "Settings Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static TableLayoutPanel BuildGrid(TabPage tab)
    {
        var layout = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock        = DockStyle.Fill,
            AutoSize    = true,
            Padding     = new Padding(10)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tab.Controls.Add(layout);
        return layout;
    }

    private static void AddRow(TableLayoutPanel layout, int row, string labelText, Control control)
    {
        while (layout.RowCount <= row)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowCount++;
        }
        if (!string.IsNullOrEmpty(labelText))
        {
            layout.Controls.Add(new Label
            {
                Text      = labelText,
                AutoSize  = true,
                Anchor    = AnchorStyles.Left | AnchorStyles.Top,
                Padding   = new Padding(0, 6, 8, 0)
            }, 0, row);
        }
        control.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        layout.Controls.Add(control, 1, row);
    }

    private static readonly Dictionary<string, string> SchemaHints = new()
    {
        ["Default"]  = "Default table: ItemLog  |  Columns: ItemDateTime, Barcode, Length, Width…",
        ["Snowsoft"]  = "Default table: tbl_Scanned_Items  |  Columns: Item_Date_Time, No_Read, Barcode…",
        ["Madibana"] = "Default table: tbl_Measurement  |  Columns: Item_Date_Time, No_Read, No_Dimension, Hand_Scanned…"
    };

    private void UpdateSchemaHint(Label label)
    {
        var key = _dbSchema.SelectedItem?.ToString() ?? "Default";
        label.Text = SchemaHints.TryGetValue(key, out var hint) ? hint : string.Empty;
    }
}
