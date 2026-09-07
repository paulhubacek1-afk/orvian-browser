#define MyAppName "Orvian Browser"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "Orvian Project"
#define MyAppExeName "Orvian.exe"

[Setup]
AppId={{8B4E3B4A-4A4E-4ED5-8C6D-7F2B9A3D1001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Orvian Browser
DefaultGroupName={#MyAppName}
OutputDir=..\dist
OutputBaseFilename=Orvian-Browser-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

[Files]
Source: "..\src\Orvian.Browser\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autodesktop}\Orvian Browser"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Orvian Browser"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Orvian Browser starten"; Flags: nowait postinstall skipifsilent
