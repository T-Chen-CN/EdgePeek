#ifndef AppVersion
#define AppVersion "0.1.1"
#endif

#ifndef SourceDir
#define SourceDir "..\artifacts\publish\EdgePeek-0.1.1-win-x64"
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

[Icons]
Name: "{group}\EdgePeek"; Filename: "{app}\EdgePeek.exe"
Name: "{autodesktop}\EdgePeek"; Filename: "{app}\EdgePeek.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\EdgePeek.exe"; Description: "Launch EdgePeek"; Flags: nowait postinstall skipifsilent

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
  FallbackDataDir: String;
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  if MsgBox('Do you want to remove EdgePeek user data?', mbConfirmation, MB_YESNO) <> IDYES then
    Exit;

  DelTree(ExpandConstant('{app}\Data'), True, True, True);

  FallbackDataDir := ExpandConstant('{localappdata}\EdgePeek\Data');
  DelTree(FallbackDataDir, True, True, True);
end;
