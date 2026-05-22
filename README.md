# EdgePeek

EdgePeek is a lightweight Windows-only slide-out browser prototype. It stays in the system tray, watches the screen edge, and slides a WebView2 browser panel into view when the cursor touches the configured hot edge.

## License

EdgePeek is open-source software licensed under the [MIT License](LICENSE).

## Current MVP

- System tray entry
- Right-edge hot-zone trigger
- Slide-in and slide-out browser panel
- WebView2-based browsing
- Address/search box
- Back, forward, refresh, home, hide
- Settings window from the tray menu
- Left or right dock edge
- Optional Ctrl+Alt+Space global hotkey
- Optional start with Windows
- New windows open in the same panel
- Last URL, tabs, panel size, language, and behavior settings are saved under `%APPDATA%\EdgePeek\settings.json`
- WebView2 profile data is stored under `%LOCALAPPDATA%\EdgePeek\WebView2` so build output stays clean
- Tab icons are loaded from WebView2 or favicon URLs declared by the current page; no third-party favicon lookup service is used

## Requirements

- Windows 10/11
- .NET 8 SDK
- Microsoft Edge WebView2 Runtime

Most Windows 11 systems already include WebView2 Runtime. If it is missing, install the Evergreen Runtime from Microsoft.

## Install Dependencies

Install .NET 8 SDK:

```powershell
winget install Microsoft.DotNet.SDK.8
```

Install WebView2 Runtime if your system does not already have it:

```powershell
winget install Microsoft.EdgeWebView2Runtime
```

Restart PowerShell after installing the SDK so `dotnet` is available on `PATH`.

## Run From Source

```powershell
git clone https://github.com/T-Chen-CN/EdgePeek.git
cd EdgePeek\EdgePeek
dotnet restore
dotnet run
```

EdgePeek starts hidden by default. Move the cursor to the configured screen edge, use the global hotkey, or double-click the tray icon to show it.
The startup behavior can be changed in Settings. To force a one-off visible launch, run `EdgePeek.exe --show`.

## Run Tests

```powershell
dotnet run --project EdgePeek.Tests\EdgePeek.Tests.csproj
```

## Build Release Artifacts

Portable zip only:

```powershell
.\scripts\publish-release.ps1 -Version 0.1.1 -SkipInstaller
```

Portable zip plus an Inno Setup installer:

```powershell
.\scripts\publish-release.ps1 -Version 0.1.1
```

Signed release artifacts:

```powershell
.\scripts\publish-release.ps1 `
  -Version 0.1.1 `
  -CertificatePath C:\certs\publisher.pfx `
  -CertificatePassword "<pfx-password>"
```

The script writes release files to `artifacts/`. If Inno Setup 6 is not installed, it still creates the portable zip and prints a warning for the installer step.

## Code Signing

EdgePeek is prepared for SignPath Foundation open-source signing through GitHub Actions.

See [CODE_SIGNING.md](CODE_SIGNING.md) for the signing policy, required repository variables, and release verification commands.

## Publish A Local Build

Framework-dependent build:

```powershell
cd EdgePeek
dotnet publish -c Release -r win-x64 --self-contained false
```

Self-contained build, larger but does not require the .NET runtime to be installed:

```powershell
cd EdgePeek
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The output will be under `bin\Release\net8.0-windows\win-x64\publish`.

## Repository Boundaries

The Git repository tracks source code, project files, manifests, README content, and shared editor/Git configuration.

The repository intentionally ignores build output, local IDE state, runtime logs, WebView2 browser profiles, and checkpoint/reference archives:

- `bin/`, `obj/`, `publish/`, `artifacts/`
- `.vs/`, `.vscode/`, `*.user`
- `*.log`, `*.tmp`
- `*.WebView2/`, `EdgePeek.exe.WebView2/`
- `checkpoints/`, `tmp-checkpoint-compare/`

Runtime user data lives outside the repository:

- Settings: `%APPDATA%\EdgePeek\settings.json`
- WebView2 profile/cache: `%LOCALAPPDATA%\EdgePeek\WebView2`

If `settings.json` cannot be read, EdgePeek backs up the corrupt file as `settings.corrupt-yyyyMMdd-HHmmss.json` before falling back to defaults.

## Next Steps

- Improve multi-monitor behavior while the panel is already visible
- Split browser tab, window placement, and settings coordination out of `MainWindow.xaml.cs`
- Expand automated coverage around WebView2 event handling and window placement edge cases
