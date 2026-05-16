using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EdgePeek;

public partial class SettingsPage : System.Windows.Controls.UserControl
{
    private readonly AppSettings _settings;
    private bool _isRecordingHotkey;

    public event EventHandler? Saved;
    public event EventHandler? BackRequested;

    public bool HasUnsavedChanges => HasChanges();

    public SettingsPage(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        LoadSettings();
        ApplyLanguage();
    }

    private void LoadSettings()
    {
        foreach (ComboBoxItem item in EdgeBox.Items)
        {
            if (string.Equals(item.Tag?.ToString(), _settings.Edge.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                EdgeBox.SelectedItem = item;
                break;
            }
        }

        foreach (ComboBoxItem item in LanguageBox.Items)
        {
            if (string.Equals(item.Tag?.ToString(), _settings.Language, StringComparison.OrdinalIgnoreCase))
            {
                LanguageBox.SelectedItem = item;
                break;
            }
        }

        TriggerBox.Text = _settings.TriggerThickness.ToString();
        HoverDelayBox.Text = _settings.EdgeHoverDelayMs.ToString();
        HomeUrlBox.Text = _settings.HomeUrl;
        HotkeyGestureBox.Text = _settings.HotkeyGesture;
        TopMostBox.IsChecked = _settings.TopMost;
        HideOnLostFocusBox.IsChecked = _settings.HideOnLostFocus;
        StartWithWindowsBox.IsChecked = _settings.StartWithWindows;
        HotkeyBox.IsChecked = _settings.EnableGlobalHotkey;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (TrySave())
        {
            Saved?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool TrySave()
    {
        if (!int.TryParse(TriggerBox.Text, out var triggerThickness) || triggerThickness < 1 || triggerThickness > 32)
        {
            ShowValidation("Hot zone must be between 1 and 32 pixels.", "热区像素必须在 1 到 32 之间。");
            return false;
        }

        if (!int.TryParse(HoverDelayBox.Text, out var hoverDelayMs) || hoverDelayMs < 0 || hoverDelayMs > 5000)
        {
            ShowValidation("Edge delay must be between 0 and 5000 milliseconds.", "靠边触发时长必须在 0 到 5000 毫秒之间。");
            return false;
        }

        var selectedLanguage = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var selectedEdge = (EdgeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        _settings.Language = string.Equals(selectedLanguage, "zh-CN", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en";
        _settings.Edge = string.Equals(selectedEdge, "Left", StringComparison.OrdinalIgnoreCase) ? DockEdge.Left : DockEdge.Right;
        _settings.TriggerThickness = triggerThickness;
        _settings.EdgeHoverDelayMs = hoverDelayMs;
        _settings.HomeUrl = NormalizeHomeUrl(HomeUrlBox.Text);
        _settings.HotkeyGesture = HotkeyGestureBox.Text;
        _settings.TopMost = TopMostBox.IsChecked == true;
        _settings.HideOnLostFocus = HideOnLostFocusBox.IsChecked == true;
        _settings.StartWithWindows = StartWithWindowsBox.IsChecked == true;
        _settings.EnableGlobalHotkey = HotkeyBox.IsChecked == true;
        return true;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            ApplyLanguage();
        }
    }

    private void RecordHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        _isRecordingHotkey = true;
        HotkeyGestureBox.Text = IsChinese() ? "请按快捷键..." : "Press shortcut...";
        Focusable = true;
        Focus();
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (!_isRecordingHotkey)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        var gesture = BuildGesture(Keyboard.Modifiers, key);
        if (gesture is null)
        {
            ShowValidation("Use at least one modifier key, such as Ctrl or Alt.", "请至少包含一个修饰键，例如 Ctrl 或 Alt。");
            return;
        }

        HotkeyGestureBox.Text = gesture;
        _isRecordingHotkey = false;
        e.Handled = true;
    }

    private static string? BuildGesture(ModifierKeys modifiers, Key key)
    {
        if (modifiers == ModifierKeys.None || key == Key.None)
        {
            return null;
        }

        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    private void ApplyLanguage()
    {
        var zh = IsChinese();
        TitleText.Text = zh ? "设置" : "Settings";
        LanguageLabel.Text = zh ? "语言" : "Language";
        DockEdgeLabel.Text = zh ? "停靠边缘" : "Dock edge";
        TriggerLabel.Text = zh ? "热区像素" : "Hot zone px";
        SetTriggerHelpText(zh
            ? "屏幕边缘的感应区域宽度。数值越大越容易触发，但也更容易误触。"
            : "Width of the screen-edge sensing area. Larger values are easier to trigger but may cause accidental popups.");
        HoverDelayLabel.Text = zh ? "靠边触发时长(ms)" : "Edge delay ms";
        HomeUrlLabel.Text = zh ? "主页地址" : "Home URL";
        HotkeyLabel.Text = zh ? "快捷键" : "Hotkey";
        RecordHotkeyButton.Content = zh ? "录制" : "Record";
        TopMostBox.Content = zh ? "保持窗口置顶" : "Keep panel above other windows";
        HideOnLostFocusBox.Content = zh ? "失去焦点时隐藏" : "Hide when focus is lost";
        StartWithWindowsBox.Content = zh ? "开机启动" : "Start with Windows";
        HotkeyBox.Content = zh ? "启用快捷键" : "Enable hotkey";
        BackButton.Content = zh ? "返回" : "Back";
        SaveButton.Content = zh ? "保存" : "Save";

        foreach (ComboBoxItem item in EdgeBox.Items)
        {
            item.Content = item.Tag?.ToString() == "Left"
                ? (zh ? "左侧" : "Left")
                : (zh ? "右侧" : "Right");
        }
    }

    public bool IsChineseLanguage()
    {
        return IsChinese();
    }

    private bool IsChinese()
    {
        var selectedLanguage = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        return string.Equals(selectedLanguage ?? _settings.Language, "zh-CN", StringComparison.OrdinalIgnoreCase);
    }

    private bool HasChanges()
    {
        var selectedLanguage = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "en";
        var selectedEdge = (EdgeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Right";

        return !string.Equals(selectedLanguage, _settings.Language, StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(selectedEdge, _settings.Edge.ToString(), StringComparison.OrdinalIgnoreCase) ||
               TriggerBox.Text.Trim() != _settings.TriggerThickness.ToString() ||
               HoverDelayBox.Text.Trim() != _settings.EdgeHoverDelayMs.ToString() ||
               NormalizeHomeUrl(HomeUrlBox.Text) != _settings.HomeUrl ||
               HotkeyGestureBox.Text != _settings.HotkeyGesture ||
               TopMostBox.IsChecked != _settings.TopMost ||
               HideOnLostFocusBox.IsChecked != _settings.HideOnLostFocus ||
               StartWithWindowsBox.IsChecked != _settings.StartWithWindows ||
               HotkeyBox.IsChecked != _settings.EnableGlobalHotkey;
    }

    private void ShowValidation(string english, string chinese)
    {
        System.Windows.MessageBox.Show(IsChinese() ? chinese : english, IsChinese() ? "设置无效" : "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void TriggerHelpButton_Click(object sender, RoutedEventArgs e)
    {
        if (TriggerHelpButton.ToolTip is System.Windows.Controls.ToolTip tooltip)
        {
            tooltip.IsOpen = true;
        }
        else
        {
            var text = TriggerHelpButton.ToolTip?.ToString() ?? string.Empty;
            TriggerHelpButton.ToolTip = new System.Windows.Controls.ToolTip
            {
                Content = text,
                IsOpen = true
            };
        }
    }

    private void SetTriggerHelpText(string text)
    {
        if (TriggerHelpButton.ToolTip is System.Windows.Controls.ToolTip tooltip)
        {
            tooltip.Content = text;
            return;
        }

        TriggerHelpButton.ToolTip = new System.Windows.Controls.ToolTip
        {
            Content = text
        };
    }

    private static string NormalizeHomeUrl(string value)
    {
        var input = value.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return "https://www.bing.com";
        }

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri.ToString();
        }

        return $"https://{input}";
    }
}
