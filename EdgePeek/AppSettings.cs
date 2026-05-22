namespace EdgePeek;

public enum DockEdge
{
    Left,
    Right
}

public sealed class AppSettings
{
    public DockEdge Edge { get; set; } = DockEdge.Right;
    public double PanelWidth { get; set; } = 460;
    public double PanelHeight { get; set; }
    public double PanelTop { get; set; }
    public int TriggerThickness { get; set; } = 4;
    public int TriggerPollingMs { get; set; } = 60;
    public int EdgeHoverDelayMs { get; set; } = 500;
    public int SlideInAnimationMs { get; set; } = 220;
    public int SlideOutAnimationMs { get; set; } = 167;
    public bool HideOnLostFocus { get; set; } = true;
    public bool TopMost { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool ShowOnStartup { get; set; } = true;
    public bool EnableGlobalHotkey { get; set; } = true;
    public string HotkeyGesture { get; set; } = "Ctrl+Alt+Space";
    public string Language { get; set; } = "en";
    public string HomeUrl { get; set; } = "https://www.bing.com";
    public string LastUrl { get; set; } = "https://www.bing.com";
    public List<string> TabUrls { get; set; } = [];
    public int ActiveTabIndex { get; set; }
}
