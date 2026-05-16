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
- Last URL and panel width are saved under `%APPDATA%\EdgePeek\settings.json`

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

## Next Steps

- Improve multi-monitor behavior while the panel is already visible
- Add a real app icon
- Add an installer
- Add configurable hotkey recording
