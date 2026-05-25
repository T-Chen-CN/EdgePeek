using System.Windows.Input;
using EdgePeek;

var tests = new (string Name, Action Body)[]
{
    ("address default", () => Equal(UrlNormalizer.DefaultUrl, UrlNormalizer.NormalizeAddress("   "))),
    ("address keeps http url", () => Equal("http://example.com/", UrlNormalizer.NormalizeAddress("http://example.com"))),
    ("address adds https to host", () => Equal("https://example.com", UrlNormalizer.NormalizeAddress("example.com"))),
    ("address searches plain text", () => Equal("https://www.bing.com/search?q=edge%20peek", UrlNormalizer.NormalizeAddress("edge peek"))),
    ("home default", () => Equal(UrlNormalizer.DefaultUrl, UrlNormalizer.NormalizeHomeUrl(""))),
    ("home adds https", () => Equal("https://example.com", UrlNormalizer.NormalizeHomeUrl("example.com"))),
    ("hotkey parses ctrl alt space", ParsesCtrlAltSpace),
    ("hotkey rejects missing modifier", () => False(HotkeyGestureParser.TryParse("Space", out _))),
    ("hotkey rejects unknown token", () => False(HotkeyGestureParser.TryParse("Ctrl+NoSuchKey", out _))),
    ("hotkey rejects multiple non-modifier keys", () => False(HotkeyGestureParser.TryParse("Ctrl+K+L", out _))),
    ("hotkey rejects modifier-only build", () => Equal(null, HotkeyGestureParser.Build(ModifierKeys.Control, Key.LeftCtrl))),
    ("hotkey builds gesture", () => Equal("Ctrl+Alt+K", HotkeyGestureParser.Build(ModifierKeys.Control | ModifierKeys.Alt, Key.K))),
    ("hot edge requires panel vertical range", HotEdgeRequiresPanelVerticalRange),
    ("hot edge accepts matching panel range", HotEdgeAcceptsMatchingPanelRange),
    ("hot edge accepts left edge", HotEdgeAcceptsLeftEdge),
    ("hot edge rejects outside trigger thickness", HotEdgeRejectsOutsideTriggerThickness),
    ("settings show panel on startup by default", () => True(new AppSettings().ShowOnStartup)),
    ("settings store saves and loads", SettingsStoreSavesAndLoads),
    ("settings store backs up corrupt settings", SettingsStoreBacksUpCorruptSettings),
    ("downloads default folder is stable", DownloadsDefaultFolderIsStable),
    ("app paths expose data children", AppPathsExposeDataChildren),
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

if (failed > 0)
{
    Console.Error.WriteLine($"{failed} test(s) failed.");
    return 1;
}

Console.WriteLine($"{tests.Length} test(s) passed.");
return 0;

static void ParsesCtrlAltSpace()
{
    True(HotkeyGestureParser.TryParse("Ctrl+Alt+Space", out var gesture));
    Equal(HotkeyGestureParser.ModControl | HotkeyGestureParser.ModAlt, gesture.Modifiers);
    Equal(Key.Space, gesture.Key);
}

static void HotEdgeRequiresPanelVerticalRange()
{
    var screen = new System.Drawing.Rectangle(0, 0, 1920, 1080);
    var panel = new System.Drawing.Rectangle(0, 300, 1920, 420);
    var cursor = new System.Drawing.Point(1919, 120);

    False(HotEdgeWatcher.IsInHotZone(cursor, screen, panel, DockEdge.Right, 4));
}

static void HotEdgeAcceptsMatchingPanelRange()
{
    var screen = new System.Drawing.Rectangle(0, 0, 1920, 1080);
    var panel = new System.Drawing.Rectangle(0, 300, 1920, 420);
    var cursor = new System.Drawing.Point(1919, 520);

    True(HotEdgeWatcher.IsInHotZone(cursor, screen, panel, DockEdge.Right, 4));
}

static void HotEdgeAcceptsLeftEdge()
{
    var screen = new System.Drawing.Rectangle(0, 0, 1920, 1080);
    var panel = new System.Drawing.Rectangle(0, 0, 1920, 1080);
    var cursor = new System.Drawing.Point(2, 520);

    True(HotEdgeWatcher.IsInHotZone(cursor, screen, panel, DockEdge.Left, 4));
}

static void HotEdgeRejectsOutsideTriggerThickness()
{
    var screen = new System.Drawing.Rectangle(0, 0, 1920, 1080);
    var panel = new System.Drawing.Rectangle(0, 0, 1920, 1080);
    var cursor = new System.Drawing.Point(1914, 520);

    False(HotEdgeWatcher.IsInHotZone(cursor, screen, panel, DockEdge.Right, 4));
}

static void SettingsStoreSavesAndLoads()
{
    using var folder = new TempFolder();
    var store = new SettingsStore(folder.Path);
    var settings = new AppSettings
    {
        Edge = DockEdge.Left,
        HomeUrl = "https://example.com",
        LastUrl = "https://example.org",
        TabUrls = ["https://example.com", "https://example.org"],
        TabViewModes = [BrowserViewMode.Desktop, BrowserViewMode.Mobile],
        ActiveTabIndex = 1
    };

    store.Save(settings);
    var loaded = store.Load();

    Equal(DockEdge.Left, loaded.Edge);
    Equal("https://example.com", loaded.HomeUrl);
    Equal("https://example.org", loaded.LastUrl);
    Equal(2, loaded.TabUrls.Count);
    Equal(2, loaded.TabViewModes.Count);
    Equal(BrowserViewMode.Mobile, loaded.TabViewModes[1]);
    Equal(1, loaded.ActiveTabIndex);
}

static void SettingsStoreBacksUpCorruptSettings()
{
    using var folder = new TempFolder();
    System.IO.File.WriteAllText(System.IO.Path.Combine(folder.Path, "settings.json"), "{ not json");

    var store = new SettingsStore(folder.Path);
    var loaded = store.Load();

    Equal(UrlNormalizer.DefaultUrl, loaded.HomeUrl);
    Equal(1, System.IO.Directory.GetFiles(folder.Path, "settings.corrupt-*.json").Length);
}

static void AppPathsExposeDataChildren()
{
    True(AppPaths.SettingsPath.StartsWith(AppPaths.DataFolder, StringComparison.OrdinalIgnoreCase));
    True(AppPaths.LogPath.StartsWith(AppPaths.DataFolder, StringComparison.OrdinalIgnoreCase));
    True(AppPaths.WebView2Folder.StartsWith(AppPaths.DataFolder, StringComparison.OrdinalIgnoreCase));
    True(System.IO.Directory.Exists(AppPaths.DataFolder));
}

static void DownloadsDefaultFolderIsStable()
{
    var settings = new AppSettings();
    Equal(AppPaths.DefaultDownloadFolder, DownloadManager.GetEffectiveDownloadFolder(settings));

    settings.DownloadFolder = "%USERPROFILE%\\Downloads\\CustomEdgePeek";
    True(DownloadManager.GetEffectiveDownloadFolder(settings).EndsWith("Downloads\\CustomEdgePeek", StringComparison.OrdinalIgnoreCase));
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"expected <{expected}> but got <{actual}>");
    }
}

static void True(bool value)
{
    if (!value)
    {
        throw new InvalidOperationException("expected true");
    }
}

static void False(bool value)
{
    if (value)
    {
        throw new InvalidOperationException("expected false");
    }
}

internal sealed class TempFolder : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"EdgePeek.Tests.{Guid.NewGuid():N}");

    public TempFolder()
    {
        System.IO.Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Path))
        {
            System.IO.Directory.Delete(Path, recursive: true);
        }
    }
}
