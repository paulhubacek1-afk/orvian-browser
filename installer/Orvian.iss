#define MyAppName "Orvian Browser"
#ifndef MyAppVersion
#define MyAppVersion "0.2.0"
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
CloseApplications=yes
CloseApplicationsFilter=Orvian.exe
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
var
  MaintenancePage: TInputOptionWizardPage;
  MaintenanceAction: Integer;
  HadExistingInstallation: Boolean;

function ExistingInstallation: Boolean;
var
  DefaultInstallRoot: String;
begin
  { {app} is not safe during the early wizard lifecycle. Use the default
    installation directory while the maintenance page is being initialized. }
  DefaultInstallRoot := AddBackslash(ExpandConstant('{autopf}')) + 'Orvian Browser';
  Result := FileExists(AddBackslash(DefaultInstallRoot) + '{#MyAppExeName}') or
            FileExists(AddBackslash(DefaultInstallRoot) + 'unins000.exe');
end;

procedure WritePostUpdateMarker;
var
  MarkerDir: String;
  MarkerFile: String;
begin
  MarkerDir := AddBackslash(ExpandConstant('{localappdata}')) + 'Orvian';
  ForceDirectories(MarkerDir);
  MarkerFile := AddBackslash(MarkerDir) + 'post-update.txt';
  SaveStringToFile(MarkerFile, '{#MyAppVersion}', False);
end;

procedure InitializeWizard;
begin
  HadExistingInstallation := ExistingInstallation;
  MaintenanceAction := 1;

  WizardForm.WelcomeLabel1.Caption := 'Willkommen bei Orvian!';
  WizardForm.WelcomeLabel2.Caption := 'Modern. Privat. Ressourcenschonend.' + #13#10 +
    'Open Source Browser für Windows x64.' + #13#10 +
    'Bei einer bestehenden Installation kannst du Orvian reparieren, aktualisieren oder deinstallieren.' + #13#10 +
    'Entwicklerkontakt: paul.hubacek1@gmail.com';
  WizardForm.WelcomeLabel1.Font.Size := 22;
  WizardForm.WelcomeLabel1.Font.Style := [fsBold];

  MaintenancePage := CreateInputOptionPage(wpWelcome,
    'Orvian ist bereits installiert',
    'Was möchtest du mit Orvian machen?',
    'Wähle eine Aktion und klicke auf Weiter:', True, False);
  MaintenancePage.Add('Orvian reparieren');
  MaintenancePage.Add('Orvian updaten');
  MaintenancePage.Add('Orvian deinstallieren');
  MaintenancePage.SelectedValueIndex := 1;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (Assigned(MaintenancePage)) and (PageID = MaintenancePage.ID) and (not HadExistingInstallation);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ResultCode: Integer;
  Uninstaller: String;
begin
  Result := True;

  if (Assigned(MaintenancePage)) and (CurPageID = MaintenancePage.ID) then
  begin
    MaintenanceAction := MaintenancePage.SelectedValueIndex;
    if MaintenanceAction = 2 then
    begin
      Uninstaller := AddBackslash(ExpandConstant('{autopf}')) + 'Orvian Browser\unins000.exe';
      if FileExists(Uninstaller) then
      begin
        if MsgBox('Orvian wird jetzt deinstalliert. Möchtest du fortfahren?', mbConfirmation, MB_YESNO) = IDYES then
        begin
          Exec(Uninstaller, '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
          WizardForm.Close;
        end;
      end
      else
        MsgBox('Der Orvian-Deinstaller wurde nicht gefunden. Bitte installiere Orvian erneut, um die Installation zu reparieren.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and (MaintenanceAction = 1) and HadExistingInstallation then
    WritePostUpdateMarker;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if Assigned(MaintenancePage) and (CurPageID = MaintenancePage.ID) then
    WizardForm.NextButton.Caption := 'Weiter';

  if Assigned(MaintenancePage) and (MaintenancePage.SelectedValueIndex = 0) then
    WizardForm.WelcomeLabel2.Caption := 'Reparatur: Orvian-Dateien werden neu installiert. Deine lokalen Daten bleiben erhalten.' + #13#10 +
      'Entwicklerkontakt: paul.hubacek1@gmail.com'
  else if Assigned(MaintenancePage) and (MaintenancePage.SelectedValueIndex = 1) then
    WizardForm.WelcomeLabel2.Caption := 'Update: Die neue Orvian-Version wird über die bestehende Installation installiert.' + #13#10 +
      'Nach dem Update zeigt Orvian eine neue Update-Begrüßung.' + #13#10 +
      'Entwicklerkontakt: paul.hubacek1@gmail.com'
  else if Assigned(MaintenancePage) and (MaintenancePage.SelectedValueIndex = 2) then
    WizardForm.WelcomeLabel2.Caption := 'Deinstallation: Orvian und seine Installation werden entfernt.' + #13#10 +
      'Lokale WebView2-Daten können nach der Deinstallation separat bestehen bleiben.'
end;
