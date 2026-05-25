using System.Windows;
using EdgePeek.Localization;

namespace EdgePeek;

public partial class MainWindow
{
    public void OpenSettings()
    {
        ShowPanel(forceFocus: true);
        _hideDelayTimer.Stop();
        _autoHideCheckTimer.Stop();

        if (SettingsHost.Visibility == Visibility.Visible && _settingsPage is not null)
        {
            _settingsPage.Focus();
            if (_isShown)
            {
                _autoHideCheckTimer.Start();
            }
            return;
        }

        _settingsPage = new SettingsPage(_settings);
        SetTopBarVisible(true);
        DownloadsPanel.Visibility = Visibility.Collapsed;
        _settingsPage.Saved += (_, _) =>
        {
            if (ApplySettings())
            {
                CloseSettingsPage(discardChanges: true);
            }
        };
        _settingsPage.BackRequested += (_, _) => TryCloseSettingsPage();

        SettingsHost.Children.Clear();
        SettingsHost.Children.Add(_settingsPage);
        BrowserHost.Visibility = Visibility.Collapsed;
        SettingsHost.Visibility = Visibility.Visible;
        if (_isShown)
        {
            _autoHideCheckTimer.Start();
        }
        _settingsPage.Focus();
    }

    private void CloseSettingsPage(bool discardChanges)
    {
        if (!discardChanges && _settingsPage?.HasUnsavedChanges == true)
        {
            ShowUnsavedSettingsPrompt(null);
            return;
        }

        HideUnsavedSettingsPrompt();
        SettingsHost.Children.Clear();
        SettingsHost.Visibility = Visibility.Collapsed;
        BrowserHost.Visibility = Visibility.Visible;
        _settingsPage = null;
        if (_isShown)
        {
            _autoHideCheckTimer.Start();
        }
    }

    private void TryCloseSettingsPage(Action? afterClose = null)
    {
        if (SettingsHost.Visibility != Visibility.Visible || _settingsPage is null)
        {
            afterClose?.Invoke();
            return;
        }

        if (!_settingsPage.HasUnsavedChanges)
        {
            CloseSettingsPage(discardChanges: true);
            afterClose?.Invoke();
            return;
        }

        ShowUnsavedSettingsPrompt(afterClose);
    }

    private void ShowUnsavedSettingsPrompt(Action? afterClose)
    {
        _pendingSettingsAction = afterClose;
        var zh = _settingsPage?.IsChineseLanguage() == true || Strings.IsChinese(_settings.Language);
        UnsavedSettingsText.Text = Strings.UnsavedSettings(zh);
        UnsavedCancelButton.Content = Strings.Cancel(zh);
        UnsavedDiscardButton.Content = Strings.Discard(zh);
        UnsavedSaveButton.Content = Strings.Save(zh);
        UnsavedSettingsPrompt.Visibility = Visibility.Visible;
    }

    private void HideUnsavedSettingsPrompt()
    {
        UnsavedSettingsPrompt.Visibility = Visibility.Collapsed;
        _pendingSettingsAction = null;
    }

    private void UnsavedCancelButton_Click(object sender, RoutedEventArgs e)
    {
        HideUnsavedSettingsPrompt();
    }

    private void UnsavedDiscardButton_Click(object sender, RoutedEventArgs e)
    {
        var next = _pendingSettingsAction;
        CloseSettingsPage(discardChanges: true);
        next?.Invoke();
    }

    private void UnsavedSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsPage?.TrySave() != true)
        {
            return;
        }

        if (!ApplySettings())
        {
            return;
        }

        var next = _pendingSettingsAction;
        CloseSettingsPage(discardChanges: true);
        next?.Invoke();
    }

    private bool ApplySettings()
    {
        EnsureCurrentScreenBounds();
        Width = ClampPanelWidth(_settings.PanelWidth);
        _settings.PanelWidth = Width;
        Topmost = _settings.TopMost;
        _hotEdgeWatcher.Reconfigure();
        _hotKeyManager.SetEnabled(_settings.EnableGlobalHotkey);
        StartupManager.SetEnabled(_settings.StartWithWindows);
        if (!_settingsStore.Save(_settings))
        {
            System.Windows.MessageBox.Show(this, "Settings could not be saved. Check the log for details.", "Settings save failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        PositionForCurrentScreen(hidden: !_isShown);
        ApplyTabRailPlacement();
        RenderTabs();
        SettingsApplied?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
