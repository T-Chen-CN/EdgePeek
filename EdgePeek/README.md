# EdgePeek

EdgePeek is a lightweight Windows-only slide-out browser prototype. It stays in the system tray, watches the screen edge, and slides a WebView2 browser panel into view when the cursor touches the configured hot edge.

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
cd E:\Codex\SlideBrowser\EdgePeek
dotnet restore
dotnet run
```

The app starts hidden. Move the cursor to the right edge of the screen or double-click the tray icon to show it.

## Publish A Local Build

Framework-dependent build:

```powershell
cd E:\Codex\SlideBrowser\EdgePeek
dotnet publish -c Release -r win-x64 --self-contained false
```

Self-contained build, larger but does not require the .NET runtime to be installed:

```powershell
cd E:\Codex\SlideBrowser\EdgePeek
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
- Add a real app icon
- Add an installer
- Add automated tests for URL normalization, settings persistence, and hotkey parsing
