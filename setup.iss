#define MyAppName "Systems One MQTT Service"
#define MyAppVersion "1.0.0"
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
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; IconIndex: 0
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; IconIndex: 0; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
; Stop existing service if upgrading
Filename: "sc.exe"; Parameters: "stop ""{#MyAppName}"""; \
  Flags: runhidden; StatusMsg: "Stopping existing service (if running)..."; Check: ServiceExists
; Delete existing service if upgrading
Filename: "sc.exe"; Parameters: "delete ""{#MyAppName}"""; \
  Flags: runhidden; StatusMsg: "Removing old service registration..."; Check: ServiceExists
; Wait for service to fully stop/delete
Filename: "cmd.exe"; Parameters: "/c timeout /t 3 /nobreak >nul"; \
  Flags: runhidden; StatusMsg: "Waiting for cleanup..."
; Create the service fresh with Production environment
Filename: "sc.exe"; Parameters: "create ""{#MyAppName}"" binPath=""{app}\{#MyAppExeName}"" start=auto obj=LocalSystem"; \
  Flags: runhidden; StatusMsg: "Installing Windows Service (as LocalSystem)..."
; Set environment variable for the service
Filename: "reg.exe"; Parameters: "add ""HKLM\SYSTEM\CurrentControlSet\Services\{#MyAppName}"" /v Environment /t REG_MULTI_SZ /d ""DOTNET_ENVIRONMENT=Production"" /f"; \
  Flags: runhidden; StatusMsg: "Configuring service environment..."
Filename: "sc.exe"; Parameters: "failure ""{#MyAppName}"" reset=86400 actions=restart/5000/restart/10000/restart/30000"; \
  Flags: runhidden; StatusMsg: "Configuring service recovery..."
Filename: "sc.exe"; Parameters: "start ""{#MyAppName}"""; \
  Flags: runhidden; StatusMsg: "Starting Windows Service..."

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop ""{#MyAppName}"""; Flags: runhidden
Filename: "sc.exe"; Parameters: "delete ""{#MyAppName}"""; Flags: runhidden

[Code]
function ServiceExists(): Boolean;
var
  ResultCode: Integer;
begin
  // sc query returns 0 if service exists
  Result := Exec('sc.exe', ExpandConstant('query "{#MyAppName}"'), '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

var
  DbPage: TInputQueryWizardPage;
  MqttPage: TInputQueryWizardPage;
  TopicPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  // Database configuration page
  DbPage := CreateInputQueryPage(wpSelectDir,
    'Database Configuration',
    'Enter the SQL Server connection details.',
    'These credentials will be stored securely on this machine.');
  DbPage.Add('Server (host:port, e.g. 192.168.1.16,1433):', False);
  DbPage.Add('Database Name:', False);
  DbPage.Add('Table Name:', False);
  DbPage.Add('Username:', False);
  DbPage.Add('Password:', False);

  // Set defaults
  DbPage.Values[0] := '192.168.1.16,1433';
  DbPage.Values[1] := 'Systems_One';
  DbPage.Values[2] := 'ItemLog';
  DbPage.Values[3] := '';
  DbPage.Values[4] := '';

  // MQTT configuration page
  MqttPage := CreateInputQueryPage(DbPage.ID,
    'MQTT Configuration',
    'Enter the MQTT broker and topic structure details.',
    'These settings configure broker connection and topic hierarchy.');
  MqttPage.Add('Broker URL (ws:// or mqtt://):', False);
  MqttPage.Add('Port (leave blank for default):', False);
  MqttPage.Add('Base Path (optional):', False);
  MqttPage.Add('Client ID:', False);
  MqttPage.Add('Username:', False);
  MqttPage.Add('Password:', False);

  // Set defaults
  MqttPage.Values[0] := 'ws://mqtt.sysone.co.za';
  MqttPage.Values[1] := '';
  MqttPage.Values[2] := '';
  MqttPage.Values[3] := 'systems-one-service';
  MqttPage.Values[4] := 'admin';
  MqttPage.Values[5] := 'admin';

  // Topic structure configuration page
  TopicPage := CreateInputQueryPage(MqttPage.ID,
    'Topic Structure Configuration',
    'Configure the hierarchical MQTT topic structure.',
    'Format: base/company/location/machine - e.g., systems-one/PEPKOR/WRH/DIM2');
  TopicPage.Add('Company (e.g., PEPKOR):', False);
  TopicPage.Add('Location (e.g., WRH):', False);
  TopicPage.Add('Machine ID (e.g., DIM2):', False);
  TopicPage.Add('Base Topic:', False);
  TopicPage.Add('Serial Number (e.g., 018389-01-3):', False);

  // Set defaults
  TopicPage.Values[0] := 'PEPKOR';
  TopicPage.Values[1] := 'WRH';
  TopicPage.Values[2] := 'DIM2';
  TopicPage.Values[3] := 'systems-one';
  TopicPage.Values[4] := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ConfigContent: String;
  ConfigPath: String;
  MqttPort: String;
  BrokerUrl: String;
begin
  if CurStep = ssPostInstall then
  begin
    BrokerUrl := MqttPage.Values[0];
    MqttPort := MqttPage.Values[1];
    
    // Auto-detect TLS based on URL scheme
    ConfigContent :=
      '{' + #13#10 +
      '  "Database": {' + #13#10 +
      '    "Server": "' + DbPage.Values[0] + '",' + #13#10 +
      '    "DatabaseName": "' + DbPage.Values[1] + '",' + #13#10 +
      '    "TableName": "' + DbPage.Values[2] + '",' + #13#10 +
      '    "Username": "' + DbPage.Values[3] + '",' + #13#10 +
      '    "Password": "' + DbPage.Values[4] + '"' + #13#10 +
      '  },' + #13#10 +
      '  "Mqtt": {' + #13#10 +
      '    "BrokerUrl": "' + BrokerUrl + '",' + #13#10;
    
    // Only add port if specified
    if MqttPort <> '' then
      ConfigContent := ConfigContent + '    "BrokerPort": ' + MqttPort + ',' + #13#10;
    
    // Add base path if specified  
    if MqttPage.Values[2] <> '' then
      ConfigContent := ConfigContent + '    "BasePath": "' + MqttPage.Values[2] + '",' + #13#10;
    
    ConfigContent := ConfigContent +
      '    "ClientId": "' + MqttPage.Values[3] + '",' + #13#10 +
      '    "Username": "' + MqttPage.Values[4] + '",' + #13#10 +
      '    "Password": "' + MqttPage.Values[5] + '",' + #13#10 +
      '    "BaseTopic": "' + TopicPage.Values[3] + '",' + #13#10 +
      '    "Company": "' + TopicPage.Values[0] + '",' + #13#10 +
      '    "Location": "' + TopicPage.Values[1] + '",' + #13#10 +
      '    "MachineId": "' + TopicPage.Values[2] + '",' + #13#10 +
      '    "SerialNumber": "' + TopicPage.Values[4] + '",' + #13#10 +
      '    "EncryptionTLS": ';
    
    // Add TLS setting based on URL scheme
    if (Pos('wss://', LowerCase(BrokerUrl)) > 0) or (Pos('mqtts://', LowerCase(BrokerUrl)) > 0) then
      ConfigContent := ConfigContent + 'true'
    else
      ConfigContent := ConfigContent + 'false';
    
    ConfigContent := ConfigContent + ',' + #13#10 +
      '    "ValidateCertificate": true' + #13#10 +
      '  }' + #13#10 +
      '}';

    ConfigPath := ExpandConstant('{app}\appsettings.Production.json');
    SaveStringToFile(ConfigPath, ConfigContent, False);
  end;
end;
