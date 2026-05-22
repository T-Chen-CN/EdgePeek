using System.IO;
using Microsoft.Web.WebView2.Core;

namespace EdgePeek.Services;

public sealed class WebViewEnvironmentFactory
{
    private Task<CoreWebView2Environment>? _environmentTask;

    public Task<CoreWebView2Environment> GetAsync()
    {
        _environmentTask ??= CreateAsync();
        return _environmentTask;
    }

    private static Task<CoreWebView2Environment> CreateAsync()
    {
        var userDataFolder = AppPaths.WebView2Folder;
        Directory.CreateDirectory(userDataFolder);

        var options = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = "--enable-features=OverlayScrollbar,FluentOverlayScrollbar",
            ScrollBarStyle = CoreWebView2ScrollbarStyle.FluentOverlay
        };

        return CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: userDataFolder, options);
    }
}
