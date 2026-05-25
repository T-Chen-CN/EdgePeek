#ifndef AppVersion
#define AppVersion "0.1.7"
#endif

#ifndef SourceDir
#define SourceDir "..\artifacts\publish\EdgePeek-0.1.7-win-x64"
#endif

#ifndef OutputDir
#define OutputDir "..\artifacts"
#endif

[Setup]
AppId={{7E00E0CA-E03F-4E1C-83F3-7CB1A0EFD65D}
AppName=EdgePeek
AppVersion={#AppVersion}
AppPublisher=EdgePeek
DefaultDirName={code:GetDefaultInstallDir}
DefaultGroupName=EdgePeek
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=EdgePeekSetup-{#AppVersion}-win-x64
SetupIconFile=..\EdgePeek\Assets\AppIcon.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\EdgePeek.exe

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Dirs]
Name: "{app}\Data"; Permissions: users-modify

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "redist\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\EdgePeek"; Filename: "{app}\EdgePeek.exe"
Name: "{autodesktop}\EdgePeek"; Filename: "{app}\EdgePeek.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\EdgePeek.exe"; Parameters: "--show"; Description: "Launch EdgePeek"; Flags: nowait postinstall skipifsilent

[Code]
function GetDefaultInstallDir(Param: String): String;
begin
  if DirExists('D:\') then
    Result := 'D:\Program Files\EdgePeek'
  else
    Result := ExpandConstant('{localappdata}\Programs\EdgePeek');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDataDir: String;
  FallbackDataDir: String;
  LocalAppDir: String;
  WERReportArchiveDir: String;
  WERReportQueueDir: String;
  WERTempDir: String;
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  DeleteFile(ExpandConstant('{userstartup}\EdgePeek.lnk'));
  DeleteFile(ExpandConstant('{commonstartup}\EdgePeek.lnk'));
  DeleteFile(ExpandConstant('{autodesktop}\EdgePeek.lnk'));
  DeleteFile(ExpandConstant('{commondesktop}\EdgePeek.lnk'));
  DelTree(ExpandConstant('{group}'), True, True, True);
  RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'EdgePeek');
  RegDeleteValue(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Run', 'EdgePeek');

  if MsgBox('Do you want to remove EdgePeek user data?', mbConfirmation, MB_YESNO) <> IDYES then
    Exit;

  DelTree(ExpandConstant('{app}\Data'), True, True, True);

  FallbackDataDir := ExpandConstant('{localappdata}\EdgePeek\Data');
  DelTree(FallbackDataDir, True, True, True);

  LocalAppDir := ExpandConstant('{localappdata}\EdgePeek');
  DelTree(LocalAppDir, True, True, True);

  AppDataDir := ExpandConstant('{userappdata}\EdgePeek');
  DelTree(AppDataDir, True, True, True);

  WERReportArchiveDir := ExpandConstant('{commonappdata}\Microsoft\Windows\WER\ReportArchive');
  DelTree(WERReportArchiveDir + '\AppCrash_EdgePeek.exe_*', True, True, True);
  DelTree(WERReportArchiveDir + '\Critical_EdgePeek.exe_*', True, True, True);

  WERReportQueueDir := ExpandConstant('{commonappdata}\Microsoft\Windows\WER\ReportQueue');
  DelTree(WERReportQueueDir + '\AppCrash_EdgePeek.exe_*', True, True, True);
  DelTree(WERReportQueueDir + '\Critical_EdgePeek.exe_*', True, True, True);

  WERTempDir := ExpandConstant('{commonappdata}\Microsoft\Windows\WER\Temp');
  DelTree(WERTempDir + '\*EdgePeek*', True, True, True);
end;

function IsUsableVersion(Version: String): Boolean;
begin
  Result := (Version <> '') and (Version <> '0.0.0.0');
end;

function IsWebView2Installed(): Boolean;
var
  Version: String;
begin
  Result :=
    (RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and IsUsableVersion(Version)) or
    (RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and IsUsableVersion(Version)) or
    (RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) and IsUsableVersion(Version));
end;

procedure InstallWebView2Runtime();
var
  ResultCode: Integer;
begin
  if IsWebView2Installed() then
    Exit;

  MsgBox('EdgePeek requires Microsoft Edge WebView2 Runtime. It will now be installed. This step requires internet access.', mbInformation, MB_OK);

  if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
  begin
    MsgBox('Microsoft Edge WebView2 Runtime could not be started. Please install it manually from Microsoft, then restart EdgePeek.', mbError, MB_OK);
    Exit;
  end;

  if (ResultCode <> 0) and not IsWebView2Installed() then
  begin
    MsgBox('Microsoft Edge WebView2 Runtime could not be installed. Please install it manually from Microsoft, then restart EdgePeek.', mbError, MB_OK);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    InstallWebView2Runtime();
end;
