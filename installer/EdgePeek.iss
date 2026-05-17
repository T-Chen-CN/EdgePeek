#ifndef AppVersion
#define AppVersion "0.1.0"
#endif

#ifndef SourceDir
#define SourceDir "..\artifacts\publish\EdgePeek-0.1.0-win-x64"
#endif

#ifndef OutputDir
#define OutputDir "..\artifacts"
#endif

[Setup]
AppId={{7E00E0CA-E03F-4E1C-83F3-7CB1A0EFD65D}
AppName=EdgePeek
AppVersion={#AppVersion}
AppPublisher=EdgePeek
DefaultDirName={localappdata}\Programs\EdgePeek
DefaultGroupName=EdgePeek
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=EdgePeekSetup-{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\EdgePeek.exe

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\EdgePeek"; Filename: "{app}\EdgePeek.exe"
Name: "{autodesktop}\EdgePeek"; Filename: "{app}\EdgePeek.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\EdgePeek.exe"; Description: "Launch EdgePeek"; Flags: nowait postinstall skipifsilent
