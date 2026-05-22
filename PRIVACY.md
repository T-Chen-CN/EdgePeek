# Privacy

EdgePeek is a local Windows desktop application.

## Local Data

EdgePeek stores settings under:

```text
<AppDir>\Data\settings.json
```

WebView2 browser profile data is stored under:

```text
<AppDir>\Data\WebView2
```

Application logs are stored under:

```text
<AppDir>\Data\edgepeek.log
```

If `<AppDir>\Data` is not writable, EdgePeek falls back to `%LOCALAPPDATA%\EdgePeek\Data`. During uninstall, users can choose whether to remove EdgePeek user data.

## Network Access

EdgePeek embeds Microsoft Edge WebView2. Pages loaded inside the browser panel make the same network requests they would make in a normal browser session.

EdgePeek itself does not run analytics, telemetry, advertising, or background upload services.

For tab icons, EdgePeek may read favicon metadata from the currently loaded page and download the icon URL declared by that page. EdgePeek does not use a third-party favicon lookup service.

## Third-Party Runtime

The app depends on Microsoft Edge WebView2 Runtime. Browser data and web compatibility behavior are provided by WebView2.
