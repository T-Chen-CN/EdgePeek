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
    ("hotkey builds gesture", () => Equal("Ctrl+Alt+K", HotkeyGestureParser.Build(ModifierKeys.Control | ModifierKeys.Alt, Key.K))),
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
