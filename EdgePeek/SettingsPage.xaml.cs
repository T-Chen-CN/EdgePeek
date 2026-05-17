using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EdgePeek.Localization;

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
        var zh = IsChinese();
        if (!int.TryParse(TriggerBox.Text, out var triggerThickness) || triggerThickness < 1 || triggerThickness > 32)
        {
            ShowValidation(Strings.HotZoneValidation(zh));
            return false;
        }

        if (!int.TryParse(HoverDelayBox.Text, out var hoverDelayMs) || hoverDelayMs < 0 || hoverDelayMs > 5000)
        {
            ShowValidation(Strings.EdgeDelayValidation(zh));
            return false;
        }

        var selectedLanguage = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var selectedEdge = (EdgeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        _settings.Language = Strings.IsChinese(selectedLanguage) ? "zh-CN" : "en";
        _settings.Edge = string.Equals(selectedEdge, "Left", StringComparison.OrdinalIgnoreCase) ? DockEdge.Left : DockEdge.Right;
        _settings.TriggerThickness = triggerThickness;
        _settings.EdgeHoverDelayMs = hoverDelayMs;
        _settings.HomeUrl = UrlNormalizer.NormalizeHomeUrl(HomeUrlBox.Text);
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
        HotkeyGestureBox.Text = Strings.PressShortcut(IsChinese());
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

        var gesture = HotkeyGestureParser.Build(Keyboard.Modifiers, key);
        if (gesture is null)
        {
            ShowValidation(Strings.HotkeyValidation(IsChinese()));
            return;
        }

        HotkeyGestureBox.Text = gesture;
        _isRecordingHotkey = false;
        e.Handled = true;
    }

    private void ApplyLanguage()
    {
        var zh = IsChinese();
        TitleText.Text = Strings.SettingsTitle(zh);
        LanguageLabel.Text = Strings.Language(zh);
        DockEdgeLabel.Text = Strings.DockEdge(zh);
        TriggerLabel.Text = Strings.HotZonePx(zh);
        SetTriggerHelpText(Strings.TriggerHelp(zh));
        HoverDelayLabel.Text = Strings.EdgeDelayMs(zh);
        HomeUrlLabel.Text = Strings.HomeUrl(zh);
        HotkeyLabel.Text = Strings.Hotkey(zh);
        RecordHotkeyButton.Content = Strings.Record(zh);
        TopMostBox.Content = Strings.TopMost(zh);
        HideOnLostFocusBox.Content = Strings.HideOnLostFocus(zh);
        StartWithWindowsBox.Content = Strings.StartWithWindows(zh);
        HotkeyBox.Content = Strings.EnableHotkey(zh);
        BackButton.Content = Strings.Back(zh);
        SaveButton.Content = Strings.Save(zh);

        foreach (ComboBoxItem item in EdgeBox.Items)
        {
            item.Content = item.Tag?.ToString() == "Left"
                ? Strings.Left(zh)
                : Strings.Right(zh);
        }
    }

    public bool IsChineseLanguage()
    {
        return IsChinese();
    }

    private bool IsChinese()
    {
        var selectedLanguage = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        return Strings.IsChinese(selectedLanguage ?? _settings.Language);
    }

    private bool HasChanges()
    {
        var selectedLanguage = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "en";
        var selectedEdge = (EdgeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Right";

        return !string.Equals(selectedLanguage, _settings.Language, StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(selectedEdge, _settings.Edge.ToString(), StringComparison.OrdinalIgnoreCase) ||
               TriggerBox.Text.Trim() != _settings.TriggerThickness.ToString() ||
               HoverDelayBox.Text.Trim() != _settings.EdgeHoverDelayMs.ToString() ||
               UrlNormalizer.NormalizeHomeUrl(HomeUrlBox.Text) != _settings.HomeUrl ||
               HotkeyGestureBox.Text != _settings.HotkeyGesture ||
               TopMostBox.IsChecked != _settings.TopMost ||
               HideOnLostFocusBox.IsChecked != _settings.HideOnLostFocus ||
               StartWithWindowsBox.IsChecked != _settings.StartWithWindows ||
               HotkeyBox.IsChecked != _settings.EnableGlobalHotkey;
    }

    private void ShowValidation(string message)
    {
        System.Windows.MessageBox.Show(message, Strings.InvalidSettingsTitle(IsChinese()), MessageBoxButton.OK, MessageBoxImage.Warning);
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

}
