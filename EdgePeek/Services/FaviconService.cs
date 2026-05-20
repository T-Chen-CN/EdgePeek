using System.IO;
using System.Net.Http;
using System.Text.Json;
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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
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
            var bytes = await TryGetPageIconAsync(webView, url)
                        ?? await TryGetHighResolutionFaviconAsync(url)
                        ?? webViewBytes;

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

    private static async Task<byte[]?> TryGetPageIconAsync(CoreWebView2 webView, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var pageUri))
        {
            return null;
        }

        try
        {
            var candidates = await GetDocumentIconCandidatesAsync(webView, pageUri);
            candidates.AddRange(await GetManifestIconCandidatesAsync(candidates, pageUri));

            foreach (var candidate in candidates
                         .Where(candidate => !IsUnsupportedIcon(candidate.Href))
                         .OrderByDescending(GetIconScore)
                         .ThenByDescending(candidate => candidate.MaxSize))
            {
                var bytes = await TryDownloadIconAsync(candidate.Href);
                if (bytes is not null)
                {
                    return bytes;
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Write(ex);
        }

        return null;
    }

    private static async Task<List<IconCandidate>> GetDocumentIconCandidatesAsync(CoreWebView2 webView, Uri pageUri)
    {
        const string script = """
            (() => Array.from(document.querySelectorAll('link[rel][href]')).map(link => ({
                rel: link.rel || '',
                href: link.href || '',
                sizes: link.sizes ? link.sizes.toString() : ''
            })))()
            """;
        var json = await webView.ExecuteScriptAsync(script);
        var candidates = JsonSerializer.Deserialize<List<IconCandidate>>(json, JsonOptions) ?? [];

        return candidates
            .Where(candidate => IsIconRel(candidate.Rel))
            .Select(candidate => candidate with { Href = ResolveHref(pageUri, candidate.Href) })
            .Where(candidate => Uri.TryCreate(candidate.Href, UriKind.Absolute, out _))
            .ToList();
    }

    private static async Task<List<IconCandidate>> GetManifestIconCandidatesAsync(IEnumerable<IconCandidate> candidates, Uri pageUri)
    {
        var manifest = candidates.FirstOrDefault(candidate => candidate.Rel.Contains("manifest", StringComparison.OrdinalIgnoreCase));
        if (manifest.Href is null)
        {
            return [];
        }

        try
        {
            var bytes = await TryDownloadIconAsync(manifest.Href);
            if (bytes is null)
            {
                return [];
            }

            var json = System.Text.Encoding.UTF8.GetString(bytes);
            var parsed = JsonSerializer.Deserialize<WebManifest>(json, JsonOptions);
            var manifestUri = Uri.TryCreate(manifest.Href, UriKind.Absolute, out var parsedManifestUri)
                ? parsedManifestUri
                : pageUri;
            return parsed?.Icons?
                .Where(icon => !string.IsNullOrWhiteSpace(icon.Src))
                .Select(icon => new IconCandidate("manifest-icon", ResolveHref(manifestUri, icon.Src), icon.Sizes ?? string.Empty))
                .Where(candidate => Uri.TryCreate(candidate.Href, UriKind.Absolute, out _))
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            AppLog.Write(ex);
            return [];
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

    private static async Task<byte[]?> TryDownloadIconAsync(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, href);
            request.Headers.UserAgent.ParseAdd("EdgePeek/0.1");
            using var response = await Client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            return bytes.Length > 0 ? bytes : null;
        }
        catch (Exception ex)
        {
            AppLog.Write(ex);
            return null;
        }
    }

    private static bool IsIconRel(string rel)
    {
        return rel.Contains("icon", StringComparison.OrdinalIgnoreCase) ||
               rel.Contains("manifest", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnsupportedIcon(string? href)
    {
        return string.IsNullOrWhiteSpace(href) ||
               href.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveHref(Uri pageUri, string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return string.Empty;
        }

        return Uri.TryCreate(pageUri, href, out var resolved) ? resolved.ToString() : href;
    }

    private static int GetIconScore(IconCandidate candidate)
    {
        var score = candidate.MaxSize;
        if (candidate.Rel.Contains("apple-touch-icon", StringComparison.OrdinalIgnoreCase))
        {
            score += 10000;
        }
        else if (candidate.Rel.Contains("manifest-icon", StringComparison.OrdinalIgnoreCase))
        {
            score += 8000;
        }
        else if (candidate.Rel.Contains("icon", StringComparison.OrdinalIgnoreCase))
        {
            score += 1000;
        }

        return score;
    }

    private readonly record struct IconCandidate(string Rel, string Href, string Sizes)
    {
        public int MaxSize => ParseMaxSize(Sizes);

        private static int ParseMaxSize(string? sizes)
        {
            if (string.IsNullOrWhiteSpace(sizes))
            {
                return 0;
            }

            var max = 0;
            foreach (var size in sizes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = size.Split('x', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out var width) &&
                    int.TryParse(parts[1], out var height))
                {
                    max = Math.Max(max, Math.Min(width, height));
                }
            }

            return max;
        }
    }

    private sealed class WebManifest
    {
        public List<WebManifestIcon>? Icons { get; set; }
    }

    private sealed class WebManifestIcon
    {
        public string? Src { get; set; }
        public string? Sizes { get; set; }
    }
}
