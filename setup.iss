#define MyAppName "Systems One MQTT Service"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Systems One"
#define MyAppExeName "Systems_One_MQTT_Service.exe"

[Setup]
AppId={{B7E3F1A2-9C4D-4E8F-A6B1-D2C3E4F5A6B7}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=installer_output
OutputBaseFilename=Systems_One_MQTT_Service_Setup
SetupIconFile=Icons\systems_one.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

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
; Create the service fresh
Filename: "sc.exe"; Parameters: "create ""{#MyAppName}"" binPath=""{app}\{#MyAppExeName}"" start=auto"; \
  Flags: runhidden; StatusMsg: "Installing Windows Service..."
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

procedure InitializeWizard;
begin
  // Database configuration page
  DbPage := CreateInputQueryPage(wpSelectDir,
    'Database Configuration',
    'Enter the SQL Server connection details.',
    'These credentials will be stored locally on this machine.');
  DbPage.Add('Server (e.g. 192.168.1.16,1433):', False);
  DbPage.Add('Database Name:', False);
  DbPage.Add('Table Name:', False);
  DbPage.Add('Username:', False);
  DbPage.Add('Password:', True);

  // Set defaults
  DbPage.Values[0] := '192.168.1.16,1433';
  DbPage.Values[1] := 'Systems_One';
  DbPage.Values[2] := 'ItemLog';
  DbPage.Values[3] := '';
  DbPage.Values[4] := '';

  // MQTT configuration page
  MqttPage := CreateInputQueryPage(DbPage.ID,
    'MQTT Configuration',
    'Enter the MQTT broker connection details.',
    'These credentials will be stored locally on this machine.');
  MqttPage.Add('Broker URL (e.g. mqtt://192.168.1.16):', False);
  MqttPage.Add('Broker Port:', False);
  MqttPage.Add('Client ID:', False);
  MqttPage.Add('Base Topic:', False);
  MqttPage.Add('Username:', False);
  MqttPage.Add('Password:', True);

  // Set defaults
  MqttPage.Values[0] := 'mqtt://192.168.1.16';
  MqttPage.Values[1] := '1883';
  MqttPage.Values[2] := 'systems-one-service';
  MqttPage.Values[3] := 'test';
  MqttPage.Values[4] := '';
  MqttPage.Values[5] := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ConfigContent: String;
  ConfigPath: String;
begin
  if CurStep = ssPostInstall then
  begin
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
      '    "BrokerUrl": "' + MqttPage.Values[0] + '",' + #13#10 +
      '    "BrokerPort": ' + MqttPage.Values[1] + ',' + #13#10 +
      '    "ClientId": "' + MqttPage.Values[2] + '",' + #13#10 +
      '    "BaseTopic": "' + MqttPage.Values[3] + '",' + #13#10 +
      '    "Username": "' + MqttPage.Values[4] + '",' + #13#10 +
      '    "Password": "' + MqttPage.Values[5] + '"' + #13#10 +
      '  }' + #13#10 +
      '}';

    ConfigPath := ExpandConstant('{app}\appsettings.Production.json');
    SaveStringToFile(ConfigPath, ConfigContent, False);
  end;
end;
