; Inno Setup script for Hurricane Beast Panel. Build with installer\build-installer.ps1.
#define AppName "Hurricane Beast Panel"
#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif
#define AppExe "HurricaneBeastPanel.exe"

[Setup]
AppId={{B7E4C1D2-5A3F-4E8B-9C61-2D7F0A8E4B15}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Anum Amir
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\release
OutputBaseFilename=HurricaneBeastPanel-Setup-{#AppVersion}
SetupIconFile=..\HurricaneBeastPanel\app.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "..\release\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remove the "start with Windows" task if the user turned it on.
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN HurricaneBeastPanel /F"; Flags: runhidden; RunOnceId: "RemoveStartupTask"

[Code]
const
  PawnSite = 'https://pawnio.eu';
  PawnWingetId = 'namazso.PawnIO';

var
  PawnPage: TInputOptionWizardPage;

function PawnInstalled: Boolean;
begin
  Result := RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\PawnIO');
end;

procedure InitializeWizard;
begin
  { Only offered when the driver is missing: CPU temperature and power need it. }
  PawnPage := CreateInputOptionPage(wpSelectTasks, 'Sensor driver',
    'The PawnIO driver is needed to show the CPU temperature.',
    'Hurricane Beast Panel reads the CPU temperature and power through PawnIO, a small free driver (pawnio.eu). It was not found on this PC. What would you like to do?',
    True, False);
  PawnPage.Add('Install PawnIO automatically (recommended)');
  PawnPage.Add('Open pawnio.eu so I can install it myself');
  PawnPage.Add('Skip it (the CPU temperature will not show)');
  PawnPage.SelectedValueIndex := 0;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = PawnPage.ID) and PawnInstalled;
end;

procedure InstallPawn;
var
  ResultCode: Integer;
begin
  WizardForm.StatusLabel.Caption := 'Installing the PawnIO driver...';
  WizardForm.ProgressGauge.Style := npbstMarquee;
  try
    if Exec(ExpandConstant('{sys}\cmd.exe'),
        '/c winget install --id ' + PawnWingetId + ' -e --silent --accept-package-agreements --accept-source-agreements',
        '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and PawnInstalled then
      Exit;
  finally
    WizardForm.ProgressGauge.Style := npbstNormal;
  end;
  { winget missing or failed: send the user to the website instead. }
  MsgBox('PawnIO could not be installed automatically. Its website will open so you can install it by hand.',
    mbInformation, MB_OK);
  ShellExec('open', PawnSite, '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if (CurStep = ssPostInstall) and not PawnInstalled then
  begin
    case PawnPage.SelectedValueIndex of
      0: InstallPawn;
      1: ShellExec('open', PawnSite, '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
    end;
  end;
end;
