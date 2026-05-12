#define MyAppName "Systems One MQTT Service"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0.0"
#endif
#define MyAppPublisher "Systems One"
#define MyAppExeName "Systems_One_MQTT_Service.exe"

[Setup]
AppId={{B7E3F1A2-9C4D-4E8F-A6B1-D2C3E4F5A6B7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/Jwagener1/Systems_One_MQTT_Service
AppSupportURL=https://github.com/Jwagener1/Systems_One_MQTT_Service/issues
AppUpdatesURL=https://github.com/Jwagener1/Systems_One_MQTT_Service/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=installer_output
OutputBaseFilename=Systems_One_MQTT_Service_Setup
SetupIconFile=Icons\systems_one.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
VersionInfoVersion={#MyAppVersion}
VersionInfoDescription=Manufacturing workstation monitoring service that publishes system metrics to MQTT
VersionInfoCopyright=Copyright © Systems One
Compression=lzma
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=120
WizardResizable=yes
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Never overwrite the live config — it is written by the installer Code section on fresh install only
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "appsettings.json"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; IconIndex: 0
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; IconIndex: 0; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
; Stop existing service before replacing binaries
Filename: "sc.exe"; Parameters: "stop ""{#MyAppName}"""; \
  Flags: runhidden; StatusMsg: "Stopping existing service..."; Check: ServiceExists
; Wait for the service process to fully release file locks
Filename: "cmd.exe"; Parameters: "/c timeout /t 4 /nobreak >nul"; \
  Flags: runhidden; StatusMsg: "Waiting for service to stop..."; Check: ServiceExists
; Remove the service registration so we can re-create it cleanly
Filename: "sc.exe"; Parameters: "delete ""{#MyAppName}"""; \
  Flags: runhidden; StatusMsg: "Removing old service registration..."; Check: ServiceExists
; Short pause to let SCM finish
Filename: "cmd.exe"; Parameters: "/c timeout /t 2 /nobreak >nul"; \
  Flags: runhidden; StatusMsg: "Waiting for cleanup..."
; Register the service (fresh install or upgrade)
Filename: "sc.exe"; Parameters: "create ""{#MyAppName}"" binPath=""{app}\{#MyAppExeName}"" start=auto obj=LocalSystem"; \
  Flags: runhidden; StatusMsg: "Registering Windows Service..."
; Configure automatic restart on failure
Filename: "sc.exe"; Parameters: "failure ""{#MyAppName}"" reset=86400 actions=restart/5000/restart/10000/restart/30000"; \
  Flags: runhidden; StatusMsg: "Configuring service recovery..."
; Start the service
Filename: "sc.exe"; Parameters: "start ""{#MyAppName}"""; \
  Flags: runhidden; StatusMsg: "Starting Windows Service..."

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop ""{#MyAppName}"""; Flags: runhidden
Filename: "cmd.exe"; Parameters: "/c timeout /t 3 /nobreak >nul"; Flags: runhidden
Filename: "sc.exe"; Parameters: "delete ""{#MyAppName}"""; Flags: runhidden

[Code]

// ---------------------------------------------------------------------------
// Helper: check whether the service is already registered
// ---------------------------------------------------------------------------
function ServiceExists(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('sc.exe', ExpandConstant('query "{#MyAppName}"'), '',
                 SW_HIDE, ewWaitUntilTerminated, ResultCode)
            and (ResultCode = 0);
end;

// ---------------------------------------------------------------------------
// Helper: read a single-line value from the existing appsettings.json.
// Returns DefaultValue when the key is absent or the file does not exist.
// Handles both  "Key": "value"  and  "Key": 1234  forms.
// ---------------------------------------------------------------------------
function ReadJsonValue(const FilePath, Key, DefaultValue: String): String;
var
  Lines: TArrayOfString;
  I: Integer;
  Line, Search, Raw: String;
  P, Q: Integer;
begin
  Result := DefaultValue;
  if not LoadStringsFromFile(FilePath, Lines) then
    Exit;
  Search := '"' + Key + '"';
  for I := 0 to GetArrayLength(Lines) - 1 do
  begin
    Line := Trim(Lines[I]);
    if Pos(Search, Line) > 0 then
    begin
      // Find the colon separator
      P := Pos(':', Line);
      if P = 0 then Continue;
      Raw := Trim(Copy(Line, P + 1, Length(Line)));
      // Remove trailing comma
      if (Length(Raw) > 0) and (Raw[Length(Raw)] = ',') then
        Raw := Copy(Raw, 1, Length(Raw) - 1);
      Raw := Trim(Raw);
      // Strip surrounding quotes when present
      if (Length(Raw) >= 2) and (Raw[1] = '"') and (Raw[Length(Raw)] = '"') then
        Raw := Copy(Raw, 2, Length(Raw) - 2);
      Result := Raw;
      Exit;
    end;
  end;
end;

// ---------------------------------------------------------------------------
// Wizard page variables
// ---------------------------------------------------------------------------
var
  IsUpgrade: Boolean;
  ExistingConfigPath: String;
  AppExePage: TInputFileWizardPage;
  AppDirPage: TInputDirWizardPage;
  DbPage: TInputQueryWizardPage;
  SchemaPage: TInputOptionWizardPage;
  MqttPage: TInputQueryWizardPage;
  TopicPage: TInputQueryWizardPage;

// ---------------------------------------------------------------------------
// Helper: escape a string for embedding inside a JSON string literal.
// Converts backslashes and double-quotes to their JSON-safe escape sequences.
// ---------------------------------------------------------------------------
function JsonEscape(const Value: String): String;
var
  I: Integer;
  C: Char;
begin
  Result := '';
  for I := 1 to Length(Value) do
  begin
    C := Value[I];
    case C of
      '\': Result := Result + '\\';
      '"': Result := Result + '\"';
    else
      if C = Chr(13) then
        Result := Result + '\r'
      else if C = Chr(10) then
        Result := Result + '\n'
      else if C = Chr(9) then
        Result := Result + '\t'
      else
        Result := Result + C;
    end;
  end;
end;

// ---------------------------------------------------------------------------
// InitializeWizard: detect upgrade and pre-populate fields from existing config
// ---------------------------------------------------------------------------
procedure InitializeWizard;
var
  InstallDir: String;
  SchemaName: String;
begin
  // Detect whether this is an upgrade by looking for the existing binary
  InstallDir    := ExpandConstant('{autopf}\{#MyAppName}');
  ExistingConfigPath := InstallDir + '\appsettings.json';
  IsUpgrade     := FileExists(ExistingConfigPath);

  // ------------------------------------------------------------------
  // App Watcher: executable to monitor (file browser)
  // ------------------------------------------------------------------
  AppExePage := CreateInputFilePage(wpSelectDir,
    'Application Watcher - Executable',
    'Select the application executable (.exe) to monitor.',
    'The service watches this process and collects metrics from its output. Click Browse to pick the .exe in Windows Explorer.');
  AppExePage.Add('Application executable:',
                 'Executable files|*.exe|All files|*.*', '.exe');

  if IsUpgrade then
    AppExePage.Values[0] := ReadJsonValue(ExistingConfigPath, 'ExePath',
      'C:\Program Files (x86)\Systems-One\App\Sys_One_Static_App.exe')
  else
    AppExePage.Values[0] := 'C:\Program Files (x86)\Systems-One\App\Sys_One_Static_App.exe';

  // ------------------------------------------------------------------
  // App Watcher: settings folder (directory browser)
  // ------------------------------------------------------------------
  AppDirPage := CreateInputDirPage(AppExePage.ID,
    'Application Watcher - Settings Folder',
    'Select the folder where the monitored application stores its settings.',
    'This folder is read by the service to enrich the published metrics.',
    False, '');
  AppDirPage.Add('Settings folder:');

  if IsUpgrade then
    AppDirPage.Values[0] := ReadJsonValue(ExistingConfigPath, 'SettingsDir',
      'C:\Users\Public\Documents\SystemOne_App_Settings')
  else
    AppDirPage.Values[0] := 'C:\Users\Public\Documents\SystemOne_App_Settings';

  // ------------------------------------------------------------------
  // Database configuration page
  // ------------------------------------------------------------------
  DbPage := CreateInputQueryPage(AppDirPage.ID,
    'Database Configuration',
    'Enter the SQL Server connection details.',
    'These credentials will be stored securely on this machine.');
  DbPage.Add('Server (host:port, e.g. 192.168.1.16,1433):', False);
  DbPage.Add('Database Name:', False);
  DbPage.Add('Username:', False);
  DbPage.Add('Password:', True);

  if IsUpgrade then
  begin
    DbPage.Values[0] := ReadJsonValue(ExistingConfigPath, 'Server',       'localhost');
    DbPage.Values[1] := ReadJsonValue(ExistingConfigPath, 'DatabaseName', 'Systems_One');
    DbPage.Values[2] := ReadJsonValue(ExistingConfigPath, 'Username',     'SysOne');
    DbPage.Values[3] := ReadJsonValue(ExistingConfigPath, 'Password',     '');
  end
  else
  begin
    DbPage.Values[0] := 'localhost';
    DbPage.Values[1] := 'Systems_One';
    DbPage.Values[2] := 'SysOne';
    DbPage.Values[3] := 'SysOne012!';
  end;

  // ------------------------------------------------------------------
  // Database schema selector page
  // ------------------------------------------------------------------
  SchemaPage := CreateInputOptionPage(DbPage.ID,
    'Database Schema',
    'Select the customer database schema to use.',
    'Each option targets a different customer''s table layout. Default uses ItemLog, Snowsoft uses tbl_Scanned_Items, Madibana uses tbl_Measurement.',
    True, False);
  SchemaPage.Add('Default (standard ItemLog schema)');
  SchemaPage.Add('Snowsoft (tbl_Scanned_Items)');
  SchemaPage.Add('Madibana (tbl_Measurement)');

  if IsUpgrade then
  begin
    SchemaName := Lowercase(ReadJsonValue(ExistingConfigPath, 'SchemaType', 'Default'));
    if SchemaName = 'snowsoft' then
      SchemaPage.SelectedValueIndex := 1
    else if SchemaName = 'madibana' then
      SchemaPage.SelectedValueIndex := 2
    else
      SchemaPage.SelectedValueIndex := 0;
  end
  else
    SchemaPage.SelectedValueIndex := 0;

  // ------------------------------------------------------------------
  // MQTT configuration page
  // ------------------------------------------------------------------
  MqttPage := CreateInputQueryPage(SchemaPage.ID,
    'MQTT Configuration',
    'Enter the MQTT broker and topic structure details.',
    'These settings configure broker connection and topic hierarchy.');
  MqttPage.Add('Broker URL (ws:// or mqtt://):', False);
  MqttPage.Add('Port (leave blank for default):', False);
  MqttPage.Add('Base Path (optional):', False);
  MqttPage.Add('Username:', False);
  MqttPage.Add('Password:', True);

  if IsUpgrade then
  begin
    MqttPage.Values[0] := ReadJsonValue(ExistingConfigPath, 'BrokerUrl',  'ws://mqtt.sysone.co.za');
    MqttPage.Values[1] := ReadJsonValue(ExistingConfigPath, 'BrokerPort', '0');
    if MqttPage.Values[1] = '0' then MqttPage.Values[1] := '';
    MqttPage.Values[2] := ReadJsonValue(ExistingConfigPath, 'BasePath',   '');
    MqttPage.Values[3] := ReadJsonValue(ExistingConfigPath, 'Username',   'admin');
    MqttPage.Values[4] := ReadJsonValue(ExistingConfigPath, 'Password',   '');
  end
  else
  begin
    MqttPage.Values[0] := 'ws://mqtt.sysone.co.za';
    MqttPage.Values[1] := '';
    MqttPage.Values[2] := '';
    MqttPage.Values[3] := 'admin';
    MqttPage.Values[4] := 'admin';
  end;

  // ------------------------------------------------------------------
  // Topic structure configuration page
  // ------------------------------------------------------------------
  TopicPage := CreateInputQueryPage(MqttPage.ID,
    'Topic Structure Configuration',
    'Configure the hierarchical MQTT topic structure.',
    'Format: base/company/location/machine — e.g., systems-one/PEPKOR/WRH/DIM2');
  TopicPage.Add('Company (e.g., PEPKOR):', False);
  TopicPage.Add('Location (e.g., WRH):', False);
  TopicPage.Add('Machine ID (e.g., DIM2):', False);
  TopicPage.Add('Base Topic:', False);
  TopicPage.Add('Serial Number (e.g., 018389-01-3):', False);

  if IsUpgrade then
  begin
    TopicPage.Values[0] := ReadJsonValue(ExistingConfigPath, 'Company',      '');
    TopicPage.Values[1] := ReadJsonValue(ExistingConfigPath, 'Location',     '');
    TopicPage.Values[2] := ReadJsonValue(ExistingConfigPath, 'MachineId',    '');
    TopicPage.Values[3] := ReadJsonValue(ExistingConfigPath, 'BaseTopic',    'systems-one');
    TopicPage.Values[4] := ReadJsonValue(ExistingConfigPath, 'SerialNumber', '');
  end
  else
  begin
    TopicPage.Values[0] := '';
    TopicPage.Values[1] := '';
    TopicPage.Values[2] := '';
    TopicPage.Values[3] := 'systems-one';
    TopicPage.Values[4] := '';
  end;
end;

// ---------------------------------------------------------------------------
// Show upgrade notice on the Welcome page
// ---------------------------------------------------------------------------
procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpWelcome) and IsUpgrade then
    WizardForm.WelcomeLabel2.Caption :=
      'This will UPGRADE the existing installation of {#MyAppName} to version {#MyAppVersion}.'
      + #13#10 + #13#10
      + 'Your current configuration has been loaded into the following pages so you can '
      + 'review and change it. The service will be stopped, updated, and restarted automatically.'
      + #13#10 + #13#10
      + WizardForm.WelcomeLabel2.Caption;
end;

// ---------------------------------------------------------------------------
// Post-install: write appsettings.json (always — user may have changed values)
// ---------------------------------------------------------------------------
procedure CurStepChanged(CurStep: TSetupStep);
var
  ConfigContent: String;
  ConfigPath: String;
  MqttPort: String;
  BrokerUrl: String;
  SchemaName: String;
begin
  if CurStep = ssPostInstall then
  begin
    BrokerUrl := MqttPage.Values[0];
    MqttPort  := MqttPage.Values[1];

    case SchemaPage.SelectedValueIndex of
      1: SchemaName := 'Snowsoft';
      2: SchemaName := 'Madibana';
    else
      SchemaName := 'Default';
    end;

    ConfigContent :=
      '{' + #13#10 +
      '  "AppCollector": {' + #13#10 +
      '    "ExePath": "' + JsonEscape(AppExePage.Values[0]) + '",' + #13#10 +
      '    "SettingsDir": "' + JsonEscape(AppDirPage.Values[0]) + '"' + #13#10 +
      '  },' + #13#10 +
      '  "Database": {' + #13#10 +
      '    "Server": "' + JsonEscape(DbPage.Values[0]) + '",' + #13#10 +
      '    "DatabaseName": "' + JsonEscape(DbPage.Values[1]) + '",' + #13#10 +
      '    "Username": "' + JsonEscape(DbPage.Values[2]) + '",' + #13#10 +
      '    "Password": "' + JsonEscape(DbPage.Values[3]) + '",' + #13#10 +
      '    "TimeoutSeconds": 30,' + #13#10 +
      '    "SchemaType": "' + SchemaName + '"' + #13#10 +
      '  },' + #13#10 +
      '  "Monitoring": {' + #13#10 +
      '    "IntervalMinutes": 5' + #13#10 +
      '  },' + #13#10 +
      '  "Mqtt": {' + #13#10 +
      '    "BrokerUrl": "' + JsonEscape(BrokerUrl) + '",' + #13#10;

    if MqttPort <> '' then
      ConfigContent := ConfigContent + '    "BrokerPort": ' + MqttPort + ',' + #13#10
    else
      ConfigContent := ConfigContent + '    "BrokerPort": 0,' + #13#10;

    ConfigContent := ConfigContent +
      '    "Username": "' + JsonEscape(MqttPage.Values[3]) + '",' + #13#10 +
      '    "Password": "' + JsonEscape(MqttPage.Values[4]) + '",' + #13#10 +
      '    "BaseTopic": "' + JsonEscape(TopicPage.Values[3]) + '",' + #13#10 +
      '    "Company": "' + JsonEscape(TopicPage.Values[0]) + '",' + #13#10 +
      '    "Location": "' + JsonEscape(TopicPage.Values[1]) + '",' + #13#10 +
      '    "MachineId": "' + JsonEscape(TopicPage.Values[2]) + '",' + #13#10 +
      '    "SerialNumber": "' + JsonEscape(TopicPage.Values[4]) + '",' + #13#10 +
      '    "BasePath": "' + JsonEscape(MqttPage.Values[2]) + '",' + #13#10 +
      '    "EncryptionTLS": ';

    if (Pos('wss://', LowerCase(BrokerUrl)) > 0) or (Pos('mqtts://', LowerCase(BrokerUrl)) > 0) then
      ConfigContent := ConfigContent + 'true'
    else
      ConfigContent := ConfigContent + 'false';

    ConfigContent := ConfigContent + ',' + #13#10 +
      '    "ValidateCertificate": true' + #13#10 +
      '  }' + #13#10 +
      '}';

    ConfigPath := ExpandConstant('{app}\appsettings.json');
    SaveStringToFile(ConfigPath, ConfigContent, False);
  end;
end;
