using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;

namespace EdgePeek.Services;

public sealed class FaviconService
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(4)
    };

    public async Task<ImageSource?> LoadAsync(CoreWebView2 webView, string url)
    {
        try
        {
            using var stream = await webView.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png);
            if (stream is null)
            {
                return null;
            }

            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            var webViewBytes = memory.ToArray();
            var bytes = await TryGetHighResolutionFaviconAsync(url) ?? webViewBytes;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(bytes);
            image.DecodePixelWidth = 32;
            image.DecodePixelHeight = 32;
            image.EndInit();
            if (image.CanFreeze)
            {
                image.Freeze();
            }

            AppLog.Write($"Favicon loaded. url={url}; bytes={bytes.Length}; webviewBytes={webViewBytes.Length}; pixel={image.PixelWidth}x{image.PixelHeight}");
            return image;
        }
        catch (Exception ex)
        {
            AppLog.Write(ex);
            return null;
        }
    }

    private static async Task<byte[]?> TryGetHighResolutionFaviconAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            return null;
        }

        try
        {
            var request = $"https://www.google.com/s2/favicons?domain={Uri.EscapeDataString(uri.Host)}&sz=64";
            var bytes = await Client.GetByteArrayAsync(request);
            return bytes.Length > 0 ? bytes : null;
        }
        catch (Exception ex)
        {
            AppLog.Write(ex);
            return null;
        }
    }
}
