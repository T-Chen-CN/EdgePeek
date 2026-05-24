using Microsoft.Web.WebView2.Wpf;
using System.Windows.Media;

namespace EdgePeek;

public sealed class BrowserTab
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Title { get; set; } = "New tab";
    public string Url { get; set; }
    public BrowserViewMode ViewMode { get; set; }
    public ImageSource? Favicon { get; set; }
    public WebView2 Browser { get; } = new();

    public BrowserTab(string url)
    {
        Url = url;
    }
}
