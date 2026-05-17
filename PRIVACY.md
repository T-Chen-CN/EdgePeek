# Privacy

EdgePeek is a local Windows desktop application.

## Local Data

EdgePeek stores settings under:

```text
%APPDATA%\EdgePeek\settings.json
```

WebView2 browser profile data is stored under:

```text
%LOCALAPPDATA%\EdgePeek\WebView2
```

Application logs are stored under:

```text
%APPDATA%\EdgePeek\edgepeek.log
```

## Network Access

EdgePeek embeds Microsoft Edge WebView2. Pages loaded inside the browser panel make the same network requests they would make in a normal browser session.

EdgePeek itself does not run analytics, telemetry, advertising, or background upload services.

## Third-Party Runtime

The app depends on Microsoft Edge WebView2 Runtime. Browser data and web compatibility behavior are provided by WebView2.
