; ============================================================================
; SmartLamp Studio - Professional Inno Setup Script
; High-performance Tuya Smart Lamp Controller (.NET 9 / WPF)
; ============================================================================

#ifndef AppVersion
  #define AppVersion "3.3.0"
#endif

#define AppName "SmartLamp Studio"
#define AppPublisher "Murr"
#define AppURL "https://github.com/vtstv/SmartLamp"
#define AppExeName "SmartLampApp.exe"

[Setup]
AppId={{D37E84B1-6284-48CA-9F93-792B6015D5CA}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} v{#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
LicenseFile=..\..\LICENSE
OutputDir=..\..\releases
OutputBaseFilename=SmartLampStudio_Setup_v{#AppVersion}_win-x64
SetupIconFile=..\..\src\Assets\app_icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; Privileges & Installation mode
PrivilegesRequiredOverridesAllowed=dialog
PrivilegesRequired=lowest

; Running process detection via Mutex
AppMutex=SmartLampApp_SingleInstanceMutex
CloseApplications=yes
RestartApplications=no

; Architectures
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Uninstaller metadata
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName} v{#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[CustomMessages]
english.AutostartTask=Start SmartLamp Studio automatically with Windows
russian.AutostartTask=Запускать SmartLamp Studio автоматически при входе в Windows
german.AutostartTask=SmartLamp Studio automatisch mit Windows starten

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "{cm:AutostartTask}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\..\dist\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\dist\app_icon.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\app_icon.ico"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\app_icon.ico"; Tasks: desktopicon

[Registry]
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SmartLampStudio"; ValueData: """{app}\{#AppExeName}"" --autostart"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
