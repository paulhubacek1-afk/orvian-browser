#define MyAppName "Orvian Browser"
#ifndef MyAppVersion
#define MyAppVersion "0.1.1"
#endif
#define MyAppPublisher "Orvian Project"
#define MyAppExeName "Orvian.exe"
#define MyRuntime "MicrosoftEdgeWebview2RuntimeInstallerX64.exe"

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
DisableWelcomePage=no
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\assets\orvian.ico

[Files]
Source: "..\src\Orvian.Browser\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\runtime\{#MyRuntime}"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{autodesktop}\Orvian Browser"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Orvian Browser"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{tmp}\{#MyRuntime}"; Parameters: "/silent /install"; StatusMsg: "WebView2 Runtime wird eingerichtet..."; Flags: waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Description: "Orvian Browser starten"; Flags: nowait postinstall skipifsilent

[Code]
procedure InitializeWizard;
begin
  WizardForm.WelcomeLabel1.Caption := 'Willkommen bei Orvian!';
  WizardForm.WelcomeLabel2.Caption := 'Dies ist ein kleines Open Source Browser Projekt!' + #13#10 +
    'Sicher. Anpassbar. Ressourcenschonend.' + #13#10 +
    'Sollte ein Fehler kommen, meldet euch in den Browser-Einstellungen unter' + #13#10 +
    '„Entwicklerkontakt“: paul.hubacek1@gmail.com';
  WizardForm.WelcomeLabel1.Font.Size := 22;
  WizardForm.WelcomeLabel1.Font.Style := [fsBold];
end;
