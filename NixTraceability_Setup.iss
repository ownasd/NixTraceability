; Inno Setup Script for NixTraceability
; Version: 1.0.0.0
; Author: Nix Industrial Solutions

#define MyAppName "NixTraceability"
#define MyAppVersion "1.0.0.0"
#define MyAppPublisher "Nix Industrial Solutions"
#define MyAppExeName "NixTraceability.exe"
#define MyAppId "NixTraceability-8273-1928-4444"

[Setup]
; App Metadata
AppId={{#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=.
OutputBaseFilename=NixTraceability_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern

; Icon path (optional, will use default if missing)
; SetupIconFile=NixTraceability\Resources\app_icon.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Source: the published EXE from the 'Publish' folder
Source: ".\Publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; Note: Database (data.db) is initialized by the app in %AppData% on first run, 
; so it doesn't need to be bundled here.

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\NixTraceability"

[Code]
// Extra logic if needed for license or pre-requisites can go here
