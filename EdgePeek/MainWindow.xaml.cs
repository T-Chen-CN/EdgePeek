using System.ComponentModel;
using System.IO;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EdgePeek.Localization;
using EdgePeek.Services;
using Forms = System.Windows.Forms;

namespace EdgePeek;

public partial class MainWindow : Window
{
    private const double ResizeHitSlop = 12;
    private const double MinPanelWidth = 320;
    private const double DefaultPanelWidth = 460;
    private const double MaxPanelScreenRatio = 0.7;
    private const double TopBarHeight = 38;
    private const double TabRailWidth = 52;

    private readonly AppSettings _settings;
    private readonly SettingsStore _settingsStore;
    private readonly HotEdgeWatcher _hotEdgeWatcher;
    private readonly HotKeyManager _hotKeyManager;
    private readonly GlobalMouseHook _globalMouseHook;
    private readonly NativeWindowAnimator _windowAnimator;
    private readonly WebViewEnvironmentFactory _webViewEnvironmentFactory = new();
    private readonly FaviconService _faviconService = new();
    private readonly DispatcherTimer _hideDelayTimer;
    private readonly DispatcherTimer _autoHideCheckTimer;
    private bool _wasPrimaryMouseDownOutside;
    private bool _isShown;
    private bool _isReallyClosing;
    private bool _isDraggingPanel;
    private bool _isTopBarHidden;
    private DateTimeOffset _lastTopBarToggleAt = DateTimeOffset.MinValue;
    private Rect _currentScreenBounds = Rect.Empty;
    private System.Windows.Point _dragStartScreenPoint;
    private double _dragStartLeft;
    private double _dragStartTop;
    private readonly List<BrowserTab> _tabs = [];
    private BrowserTab? _activeTab;
    private SettingsPage? _settingsPage;
    private Action? _pendingSettingsAction;

    private Microsoft.Web.WebView2.Wpf.WebView2? ActiveBrowser => _activeTab?.Browser;

    public event EventHandler? SettingsApplied;

    public MainWindow(AppSettings settings, SettingsStore settingsStore)
    {
        _settings = settings;
        _settingsStore = settingsStore;

        InitializeComponent();
        LoadWindowIcon();

        CaptureCurrentScreenBounds();
        Width = ClampPanelWidth(_settings.PanelWidth);
        Height = ClampPanelHeight(_settings.PanelHeight);
        Top = ClampPanelTop(_settings.PanelTop, Height);
        _settings.PanelWidth = Width;
        _settings.PanelHeight = Height;
        _settings.PanelTop = Top;
        Topmost = _settings.TopMost;
        AddressBox.Text = _settings.LastUrl;
        ApplyTabRailPlacement();

        _hideDelayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(220)
        };
        _hideDelayTimer.Tick += (_, _) =>
        {
            _hideDelayTimer.Stop();
            if (ShouldAutoHide())
            {
                HidePanel();
            }
        };

        _autoHideCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _autoHideCheckTimer.Tick += (_, _) =>
        {
            if (ShouldAutoHideOnOutsideClick())
            {
                HidePanel();
            }
            else
            {
                _wasPrimaryMouseDownOutside = IsPrimaryMouseDown() && !IsMouseInsidePanel();
            }
        };

        _hotEdgeWatcher = new HotEdgeWatcher(_settings, GetPanelHotZoneBounds);
        _hotEdgeWatcher.HotEdgeReached += (_, _) => ShowPanel(forceFocus: false);

        _hotKeyManager = new HotKeyManager(this, _settings);
        _hotKeyManager.Pressed += (_, _) => TogglePanel();

        _globalMouseHook = new GlobalMouseHook();
        _globalMouseHook.MouseDown += (_, _) =>
        {
            if (ShouldAutoHideFromExternalClick())
            {
                Dispatcher.BeginInvoke(HidePanel);
            }
        };

        _windowAnimator = new NativeWindowAnimator(this);

        Loaded += MainWindow_Loaded;
        SourceInitialized += MainWindow_SourceInitialized;
        Deactivated += MainWindow_Deactivated;
        SizeChanged += MainWindow_SizeChanged;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        WindowChromeHelper.HideFromAltTab(this);
    }

    private void LoadWindowIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (!File.Exists(iconPath))
        {
            return;
        }

        try
        {
            Icon = BitmapFrame.Create(new System.Uri(iconPath, System.UriKind.Absolute));
        }
        catch (Exception ex)
        {
            AppLog.Write("Window icon load failed.");
            AppLog.Write(ex);
        }
    }

    public async void ShowPanel(bool forceFocus)
    {
        AppLog.Write($"ShowPanel requested. forceFocus={forceFocus}");
        if (_isShown)
        {
            if (forceFocus)
            {
                Activate();
            }
            return;
        }

        _isShown = true;
        _hotEdgeWatcher.Pause();
        CaptureCurrentScreenBounds();
        PositionForCurrentScreen(hidden: true);
        Opacity = 0.96;
        Show();
        SetTopBarVisible(true);
        _autoHideCheckTimer.Start();
        AnimateOpacity(1, 120);
        AnimateTo(GetVisibleLeft(_currentScreenBounds), _settings.SlideInAnimationMs, WindowAnimationEasing.EaseOutCubic);

        if (forceFocus)
        {
            Activate();
        }

        try
        {
            if (ActiveBrowser?.CoreWebView2 is null && ActiveBrowser is not null)
            {
                await ActiveBrowser.EnsureCoreWebView2Async(await _webViewEnvironmentFactory.GetAsync());
            }
        }
        catch (Exception ex)
        {
            AppLog.Write(ex);
            System.Windows.MessageBox.Show(this, ex.Message, "WebView2 failed to start", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void HidePanel()
    {
        AppLog.Write("HidePanel requested.");
        if (!_isShown)
        {
            return;
        }

        _isShown = false;
        _hideDelayTimer.Stop();
        _autoHideCheckTimer.Stop();
        AnimateOpacity(0.9, 120);
        AnimateTo(GetHiddenLeft(_currentScreenBounds), _settings.SlideOutAnimationMs, WindowAnimationEasing.EaseInCubic, completed: () =>
        {
            Opacity = 1;
            _hotEdgeWatcher.Resume();
        });
    }

    public void TogglePanel()
    {
        if (_isShown)
        {
            HidePanel();
        }
        else
        {
            ShowPanel(forceFocus: true);
        }
    }

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
        _settingsPage.Saved += (_, _) =>
        {
            ApplySettings();
            CloseSettingsPage(discardChanges: true);
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

        ApplySettings();
        var next = _pendingSettingsAction;
        CloseSettingsPage(discardChanges: true);
        next?.Invoke();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        AppLog.Write("MainWindow loaded.");
        PositionForCurrentScreen(hidden: true);
        _hotEdgeWatcher.Start();
        _globalMouseHook.Start();
        _hotKeyManager.SetEnabled(_settings.EnableGlobalHotkey);
        StartupManager.SetEnabled(_settings.StartWithWindows);

        RestoreTabs();
    }

    private void MainWindow_Deactivated(object? sender, EventArgs e)
    {
        if (_settings.HideOnLostFocus && _isShown)
        {
            ScheduleHide();
        }
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        EnsureCurrentScreenBounds();
        var clampedWidth = ClampPanelWidth(Width);
        if (Math.Abs(Width - clampedWidth) > 0.5)
        {
            Width = clampedWidth;
            return;
        }

        _settings.PanelWidth = clampedWidth;
        _settings.PanelHeight = ClampPanelHeight(Height);
        _settings.PanelTop = ClampPanelTop(Top, Height);
        _settingsStore.Save(_settings);

        if (_isShown)
        {
            EnsureCurrentScreenBounds();
            Left = GetVisibleLeft(_currentScreenBounds);
        }
        else
        {
            EnsureCurrentScreenBounds();
            Left = GetHiddenLeft(_currentScreenBounds);
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_isReallyClosing)
        {
            return;
        }

        e.Cancel = true;
        HidePanel();
    }

    protected override void OnClosed(EventArgs e)
    {
        _hotEdgeWatcher.Stop();
        _globalMouseHook.Dispose();
        _windowAnimator.Stop();
        _autoHideCheckTimer.Stop();
        _hotKeyManager.Dispose();
        base.OnClosed(e);
    }

    public void CloseForExit()
    {
        _isReallyClosing = true;
        Close();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        TryCloseSettingsPage(() =>
        {
            if (ActiveBrowser?.CanGoBack == true)
            {
                ActiveBrowser.GoBack();
            }
        });
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        TryCloseSettingsPage(() =>
        {
            if (ActiveBrowser?.CanGoForward == true)
            {
                ActiveBrowser.GoForward();
            }
        });
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        TryCloseSettingsPage(() => ActiveBrowser?.Reload());
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e)
    {
        TryCloseSettingsPage(() => NavigateTo(_settings.HomeUrl));
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsHost.Visibility == Visibility.Visible && _settingsPage is not null)
        {
            TryCloseSettingsPage();
            return;
        }

        OpenSettings();
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        HidePanel();
    }

    private void TabRail_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.Button)
        {
            return;
        }

        _isDraggingPanel = true;
        _dragStartScreenPoint = GetMouseScreenPointInDip(e);
        _dragStartLeft = Left;
        _dragStartTop = Top;
        _hideDelayTimer.Stop();
        _autoHideCheckTimer.Stop();
        _hotEdgeWatcher.Pause();
        _windowAnimator.Stop();
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void TabRail_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDraggingPanel || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentScreenPoint = GetMouseScreenPointInDip(e);
        Left = _dragStartLeft + currentScreenPoint.X - _dragStartScreenPoint.X;
        Top = _dragStartTop + currentScreenPoint.Y - _dragStartScreenPoint.Y;
        e.Handled = true;
    }

    private void TabRail_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingPanel)
        {
            return;
        }

        _isDraggingPanel = false;
        ((UIElement)sender).ReleaseMouseCapture();
        SnapToNearestEdge();

        if (_isShown)
        {
            _autoHideCheckTimer.Start();
        }
        else
        {
            _hotEdgeWatcher.Resume();
        }

        e.Handled = true;
    }

    private void AddressBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var input = AddressBox.Text;
            TryCloseSettingsPage(() => NavigateTo(input));
        }
    }

    private void NavigateTo(string input)
    {
        if (ActiveBrowser is null)
        {
            return;
        }

        var url = UrlNormalizer.NormalizeAddress(input);
        AddressBox.Text = url;
        ActiveBrowser.Source = new Uri(url);
    }

    private void UpdateNavButtons()
    {
        BackButton.IsEnabled = ActiveBrowser?.CanGoBack == true;
        ForwardButton.IsEnabled = ActiveBrowser?.CanGoForward == true;
    }

    private void NewTabButton_Click(object sender, RoutedEventArgs e)
    {
        TryCloseSettingsPage(() => AddTabAsync(_settings.HomeUrl, activate: true).Forget("Create tab from button"));
    }

    private void RestoreTabs()
    {
        var urls = _settings.TabUrls.Count > 0 ? _settings.TabUrls : [_settings.LastUrl];
        var activeIndex = Math.Clamp(_settings.ActiveTabIndex, 0, Math.Max(0, urls.Count - 1));

        for (var index = 0; index < urls.Count; index++)
        {
            AddTabAsync(urls[index], activate: index == activeIndex).Forget("Restore tab");
        }
    }

    private async Task<BrowserTab> AddTabAsync(string url, bool activate)
    {
        var normalizedUrl = UrlNormalizer.NormalizeAddress(url);
        var tab = new BrowserTab(normalizedUrl)
        {
            Title = GetTitleFromUrl(normalizedUrl)
        };

        _tabs.Add(tab);
        ConfigureTab(tab);
        BrowserHost.Children.Add(tab.Browser);
        RenderTabs();

        try
        {
            await tab.Browser.EnsureCoreWebView2Async(await _webViewEnvironmentFactory.GetAsync());
            tab.Browser.Source = new Uri(normalizedUrl);
        }
        catch (Exception ex)
        {
            AppLog.Write(ex);
            System.Windows.MessageBox.Show(this, ex.Message, "WebView2 failed to start", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        if (activate)
        {
            ActivateTab(tab);
        }

        SaveTabs();
        return tab;
    }

    private void ConfigureTab(BrowserTab tab)
    {
        tab.Browser.Visibility = Visibility.Collapsed;
        tab.Browser.NavigationCompleted += (_, _) =>
        {
            if (tab.Browser.Source is null)
            {
                return;
            }

            tab.Url = tab.Browser.Source.ToString();
            tab.Title = GetTitleFromUrl(tab.Url);
            if (ReferenceEquals(tab, _activeTab))
            {
                AddressBox.Text = tab.Url;
                UpdateNavButtons();
            }
            RenderTabs();
            SaveTabs();
            InjectBrowserEnhancementScript(tab);
        };

        tab.Browser.CoreWebView2InitializationCompleted += (_, args) =>
        {
            if (!args.IsSuccess || tab.Browser.CoreWebView2 is null)
            {
                return;
            }

            tab.Browser.CoreWebView2.NewWindowRequested += (_, request) =>
            {
                request.Handled = true;
                AddTabAsync(request.Uri, activate: true).Forget("Open requested new window");
            };

            tab.Browser.CoreWebView2.WebMessageReceived += (_, messageArgs) =>
            {
                HandleBrowserWebMessage(messageArgs.TryGetWebMessageAsString());
            };

            tab.Browser.CoreWebView2
                .AddScriptToExecuteOnDocumentCreatedAsync(BrowserEnhancementScriptProvider.Script)
                .Forget("Register browser enhancement script");
            InjectBrowserEnhancementScript(tab);

            tab.Browser.CoreWebView2.DocumentTitleChanged += (_, _) =>
            {
                var title = tab.Browser.CoreWebView2.DocumentTitle;
                if (!string.IsNullOrWhiteSpace(title))
                {
                    tab.Title = title;
                    RenderTabs();
                }
            };

            tab.Browser.CoreWebView2.FaviconChanged += (_, _) => UpdateFaviconAsync(tab).Forget("Update favicon");
        };
    }

    private static void InjectBrowserEnhancementScript(BrowserTab tab)
    {
        if (tab.Browser.CoreWebView2 is null)
        {
            return;
        }

        tab.Browser.CoreWebView2
            .ExecuteScriptAsync(BrowserEnhancementScriptProvider.Script)
            .Forget("Inject browser enhancement script");
    }

    private void HandleBrowserWebMessage(string message)
    {
        if (!_isShown || SettingsHost.Visibility == Visibility.Visible)
        {
            return;
        }

        if (string.Equals(message, "edgepeek-scroll:down", StringComparison.Ordinal))
        {
            SetTopBarVisible(false);
        }
        else if (string.Equals(message, "edgepeek-scroll:up", StringComparison.Ordinal))
        {
            SetTopBarVisible(true);
        }
    }

    private void SetTopBarVisible(bool visible)
    {
        if (_isTopBarHidden == !visible)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        if (now - _lastTopBarToggleAt < TimeSpan.FromMilliseconds(220))
        {
            return;
        }
        _lastTopBarToggleAt = now;
        _isTopBarHidden = !visible;

        TopBar.BeginAnimation(HeightProperty, null);
        TopBar.BeginAnimation(OpacityProperty, null);

        if (visible)
        {
            TopBar.Visibility = Visibility.Visible;
        }

        var heightAnimation = new DoubleAnimation
        {
            To = visible ? TopBarHeight : 0,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new QuadraticEase { EasingMode = visible ? EasingMode.EaseOut : EasingMode.EaseIn },
            FillBehavior = FillBehavior.Stop
        };
        heightAnimation.Completed += (_, _) =>
        {
            TopBar.Height = visible ? TopBarHeight : 0;
            TopBar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        };

        var opacityAnimation = new DoubleAnimation
        {
            To = visible ? 1 : 0,
            Duration = TimeSpan.FromMilliseconds(120),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        opacityAnimation.Completed += (_, _) => TopBar.Opacity = visible ? 1 : 0;

        TopBar.BeginAnimation(HeightProperty, heightAnimation);
        TopBar.BeginAnimation(OpacityProperty, opacityAnimation);
    }

    private async Task UpdateFaviconAsync(BrowserTab tab)
    {
        if (tab.Browser.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            var image = await _faviconService.LoadAsync(tab.Browser.CoreWebView2, tab.Url);
            if (image is null)
            {
                return;
            }

            tab.Favicon = image;
            RenderTabs();
        }
        catch (Exception ex)
        {
            AppLog.Write(ex);
        }
    }

    private void ActivateTab(BrowserTab tab)
    {
        _activeTab = tab;
        foreach (var browser in _tabs.Select(item => item.Browser))
        {
            browser.Visibility = ReferenceEquals(browser, tab.Browser) ? Visibility.Visible : Visibility.Collapsed;
        }
        AddressBox.Text = tab.Url;
        UpdateNavButtons();
        RenderTabs();
        SaveTabs();
    }

    private void CloseTab(BrowserTab tab)
    {
        var index = _tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        var wasActive = ReferenceEquals(tab, _activeTab);
        BrowserHost.Children.Remove(tab.Browser);
        tab.Browser.Dispose();
        _tabs.RemoveAt(index);

        if (_tabs.Count == 0)
        {
            AddTabAsync(_settings.HomeUrl, activate: true).Forget("Create replacement tab");
            return;
        }

        if (wasActive)
        {
            ActivateTab(_tabs[Math.Clamp(index - 1, 0, _tabs.Count - 1)]);
        }
        else
        {
            RenderTabs();
            SaveTabs();
        }
    }

    private void RenderTabs()
    {
        var tabStrip = GetActiveTabStrip();
        LeftTabStrip.Items.Clear();
        RightTabStrip.Items.Clear();

        foreach (var tab in _tabs)
        {
            var selected = ReferenceEquals(tab, _activeTab);
            var button = new System.Windows.Controls.Button
            {
                Width = 44,
                Height = 42,
                Margin = new Thickness(0, 0, 0, 2),
                Padding = new Thickness(0),
                Tag = tab,
                Background = selected ? (System.Windows.Media.Brush)FindResource("SurfaceBg") : System.Windows.Media.Brushes.Transparent,
                BorderBrush = System.Windows.Media.Brushes.Transparent
            };

            var layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });

            var indicator = new Border
            {
                Width = 22,
                Height = 2,
                CornerRadius = new CornerRadius(2),
                Background = selected ? (System.Windows.Media.Brush)FindResource("AccentBrush") : System.Windows.Media.Brushes.Transparent,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetRow(indicator, 1);

            FrameworkElement tabContent;
            if (tab.Favicon is not null)
            {
                var image = new System.Windows.Controls.Image
                {
                    Source = tab.Favicon,
                    Width = 24,
                    Height = 24,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                tabContent = image;
            }
            else
            {
                tabContent = new TextBlock
                {
                    Text = GetTabInitial(tab.Title),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 17,
                    FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = selected ? (System.Windows.Media.Brush)FindResource("TextMain") : (System.Windows.Media.Brush)FindResource("TextMuted")
                };
            }
            Grid.SetRow(tabContent, 0);

            layout.Children.Add(tabContent);
            layout.Children.Add(indicator);

            button.Content = layout;
            button.ToolTip = $"{tab.Title}\nDouble-click to close";
            button.Click += (_, _) => TryCloseSettingsPage(() => ActivateTab(tab));
            button.MouseDoubleClick += (_, args) =>
            {
                args.Handled = true;
                TryCloseSettingsPage(() => CloseTab(tab));
            };
            tabStrip.Items.Add(button);
        }
    }

    private ItemsControl GetActiveTabStrip()
    {
        return _settings.Edge == DockEdge.Right ? LeftTabStrip : RightTabStrip;
    }

    private void ApplyTabRailPlacement()
    {
        var showLeft = _settings.Edge == DockEdge.Right;
        LeftTabColumn.Width = showLeft ? new GridLength(TabRailWidth) : new GridLength(0);
        RightTabColumn.Width = showLeft ? new GridLength(0) : new GridLength(TabRailWidth);
        LeftTabRail.Visibility = showLeft ? Visibility.Visible : Visibility.Collapsed;
        RightTabRail.Visibility = showLeft ? Visibility.Collapsed : Visibility.Visible;
        LeftTabRail.Opacity = showLeft ? 1 : 0;
        RightTabRail.Opacity = showLeft ? 0 : 1;
    }

    private void AnimateTabRailPlacement()
    {
        var oldRail = _settings.Edge == DockEdge.Right ? RightTabRail : LeftTabRail;
        var newRail = _settings.Edge == DockEdge.Right ? LeftTabRail : RightTabRail;

        oldRail.BeginAnimation(OpacityProperty, null);
        newRail.BeginAnimation(OpacityProperty, null);

        var fadeOut = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(80),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };

        fadeOut.Completed += (_, _) =>
        {
            ApplyTabRailPlacement();
            RenderTabs();
            newRail.Opacity = 0;
            newRail.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromMilliseconds(140),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            fadeIn.Completed += (_, _) => newRail.Opacity = 1;
            newRail.BeginAnimation(OpacityProperty, fadeIn);
        };

        oldRail.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void SaveTabs()
    {
        _settings.TabUrls = _tabs.Select(tab => tab.Url).Where(url => !string.IsNullOrWhiteSpace(url)).ToList();
        _settings.ActiveTabIndex = _activeTab is null ? 0 : Math.Max(0, _tabs.IndexOf(_activeTab));
        _settings.LastUrl = _activeTab?.Url ?? _settings.LastUrl;
        _settingsStore.Save(_settings);
    }

    private static string GetTitleFromUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return string.IsNullOrWhiteSpace(uri.Host) ? "New tab" : uri.Host;
        }

        return "New tab";
    }

    private static string GetTabInitial(string title)
    {
        var value = string.IsNullOrWhiteSpace(title) ? "N" : title.Trim();
        return value[..1].ToUpperInvariant();
    }

    private void ApplySettings()
    {
        EnsureCurrentScreenBounds();
        Width = ClampPanelWidth(_settings.PanelWidth);
        _settings.PanelWidth = Width;
        Topmost = _settings.TopMost;
        _hotEdgeWatcher.Reconfigure();
        _hotKeyManager.SetEnabled(_settings.EnableGlobalHotkey);
        StartupManager.SetEnabled(_settings.StartWithWindows);
        _settingsStore.Save(_settings);
        PositionForCurrentScreen(hidden: !_isShown);
        ApplyTabRailPlacement();
        RenderTabs();
        SettingsApplied?.Invoke(this, EventArgs.Empty);
    }

    private void ScheduleHide()
    {
        if (!_hideDelayTimer.IsEnabled)
        {
            _hideDelayTimer.Start();
        }
    }

    private bool ShouldAutoHide()
    {
        if (!_settings.HideOnLostFocus || !_isShown || _isReallyClosing)
        {
            return false;
        }

        if (_isDraggingPanel)
        {
            return false;
        }

        if (IsMouseInsidePanelOrResizeBand())
        {
            return false;
        }

        return !IsActive && !IsKeyboardFocusWithin;
    }

    private bool ShouldAutoHideOnOutsideClick()
    {
        if (!_settings.HideOnLostFocus || !_isShown || _isReallyClosing)
        {
            return false;
        }

        if (_isDraggingPanel)
        {
            return false;
        }

        if (IsMouseInsidePanelOrResizeBand())
        {
            _wasPrimaryMouseDownOutside = false;
            return false;
        }

        var isPrimaryMouseDown = IsPrimaryMouseDown();
        var clickedOutside = _wasPrimaryMouseDownOutside && !isPrimaryMouseDown;
        _wasPrimaryMouseDownOutside = isPrimaryMouseDown;

        return clickedOutside && !IsKeyboardFocusWithin;
    }

    private bool ShouldAutoHideFromExternalClick()
    {
        return _settings.HideOnLostFocus &&
               _isShown &&
               !_isReallyClosing &&
               !_isDraggingPanel &&
               !IsMouseInsidePanelOrResizeBand();
    }

    private bool IsMouseInsidePanel()
    {
        return IsMouseInsidePanelOrResizeBand(hitSlop: 0);
    }

    private bool IsMouseInsidePanelOrResizeBand()
    {
        return IsMouseInsidePanelOrResizeBand(ResizeHitSlop);
    }

    private bool IsMouseInsidePanelOrResizeBand(double hitSlop)
    {
        var cursor = Forms.Cursor.Position;
        var point = PointFromScreen(new System.Windows.Point(cursor.X, cursor.Y));
        return point.X >= -hitSlop &&
               point.Y >= -hitSlop &&
               point.X <= ActualWidth + hitSlop &&
               point.Y <= ActualHeight + hitSlop;
    }

    private static bool IsPrimaryMouseDown()
    {
        return Forms.Control.MouseButtons.HasFlag(Forms.MouseButtons.Left) ||
               Forms.Control.MouseButtons.HasFlag(Forms.MouseButtons.Right) ||
               Forms.Control.MouseButtons.HasFlag(Forms.MouseButtons.Middle);
    }

    private void PositionForCurrentScreen(bool hidden)
    {
        EnsureCurrentScreenBounds();

        Height = ClampPanelHeight(Height > 0 ? Height : _settings.PanelHeight);
        Top = ClampPanelTop(_settings.PanelTop, Height);
        Left = hidden ? GetHiddenLeft(_currentScreenBounds) : GetVisibleLeft(_currentScreenBounds);
    }

    private void EnsureCurrentScreenBounds()
    {
        if (_currentScreenBounds == Rect.Empty ||
            double.IsInfinity(_currentScreenBounds.Left) ||
            double.IsInfinity(_currentScreenBounds.Right) ||
            _currentScreenBounds.Width <= 0 ||
            _currentScreenBounds.Height <= 0)
        {
            CaptureCurrentScreenBounds();
        }
    }

    private void CaptureCurrentScreenBounds()
    {
        var cursor = Forms.Cursor.Position;
        var screen = Forms.Screen.FromPoint(cursor);
        _currentScreenBounds = ToDipRect(screen.WorkingArea);
    }

    private void CaptureScreenBoundsFromWindowCenter()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var centerX = (int)Math.Round((Left + (ActualWidth / 2)) * dpi.DpiScaleX);
        var centerY = (int)Math.Round((Top + (ActualHeight / 2)) * dpi.DpiScaleY);
        var screen = Forms.Screen.FromPoint(new System.Drawing.Point(centerX, centerY));
        _currentScreenBounds = ToDipRect(screen.WorkingArea);
    }

    private void SnapToNearestEdge()
    {
        CaptureScreenBoundsFromWindowCenter();

        var center = Left + (GetPanelWidth() / 2);
        var distanceToLeft = Math.Abs(center - _currentScreenBounds.Left);
        var distanceToRight = Math.Abs(_currentScreenBounds.Right - center);
        var nextEdge = distanceToLeft <= distanceToRight ? DockEdge.Left : DockEdge.Right;
        var edgeChanged = _settings.Edge != nextEdge;
        _settings.Edge = nextEdge;

        if (edgeChanged)
        {
            AnimateTabRailPlacement();
        }
        else
        {
            ApplyTabRailPlacement();
            RenderTabs();
        }
        Height = ClampPanelHeight(Height);
        Top = ClampPanelTop(Top, Height);
        _settings.PanelHeight = Height;
        _settings.PanelTop = Top;

        var targetLeft = _isShown ? GetVisibleLeft(_currentScreenBounds) : GetHiddenLeft(_currentScreenBounds);
        AnimateTo(targetLeft, _settings.SlideInAnimationMs, WindowAnimationEasing.EaseOutCubic, completed: () =>
        {
            _settingsStore.Save(_settings);
            if (_isShown)
            {
                _hotEdgeWatcher.Pause();
            }
            else
            {
                _hotEdgeWatcher.Resume();
            }
        });
    }

    private double GetVisibleLeft(Rect bounds)
    {
        var width = GetPanelWidth();
        return _settings.Edge == DockEdge.Left ? bounds.Left : bounds.Right - width;
    }

    private double GetHiddenLeft(Rect bounds)
    {
        var width = GetPanelWidth();
        return _settings.Edge == DockEdge.Left ? bounds.Left - width + 1 : bounds.Right - 1;
    }

    private double GetPanelWidth()
    {
        if (IsUsableLength(ActualWidth))
        {
            return ActualWidth;
        }

        if (IsUsableLength(Width))
        {
            return Width;
        }

        return ClampPanelWidth(_settings.PanelWidth);
    }

    private double GetPanelHeight()
    {
        if (IsUsableLength(ActualHeight))
        {
            return ActualHeight;
        }

        if (IsUsableLength(Height))
        {
            return Height;
        }

        return _settings.PanelHeight;
    }

    private static bool IsUsableLength(double value)
    {
        return value > 0 && IsFiniteNumber(value);
    }

    private static bool IsFiniteNumber(double value)
    {
        return !double.IsInfinity(value) && !double.IsNaN(value);
    }

    private double ClampPanelWidth(double width)
    {
        EnsureCurrentScreenBounds();
        var maxWidth = Math.Max(MinPanelWidth, _currentScreenBounds.Width * MaxPanelScreenRatio);
        if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
        {
            return Math.Min(DefaultPanelWidth, maxWidth);
        }

        return Math.Clamp(width, MinPanelWidth, maxWidth);
    }

    private double ClampPanelHeight(double height)
    {
        EnsureCurrentScreenBounds();
        var minHeight = Math.Min(MinHeight, _currentScreenBounds.Height);
        if (double.IsNaN(height) || double.IsInfinity(height) || height <= 0)
        {
            return _currentScreenBounds.Height;
        }

        return Math.Clamp(height, minHeight, _currentScreenBounds.Height);
    }

    private double ClampPanelTop(double top, double height)
    {
        EnsureCurrentScreenBounds();
        var maxTop = Math.Max(_currentScreenBounds.Top, _currentScreenBounds.Bottom - height);
        if (double.IsNaN(top) || double.IsInfinity(top))
        {
            return _currentScreenBounds.Top;
        }

        return Math.Clamp(top, _currentScreenBounds.Top, maxTop);
    }

    private Rect ToDipRect(System.Drawing.Rectangle rectangle)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        return new Rect(
            rectangle.Left / dpi.DpiScaleX,
            rectangle.Top / dpi.DpiScaleY,
            rectangle.Width / dpi.DpiScaleX,
            rectangle.Height / dpi.DpiScaleY);
    }

    private System.Drawing.Rectangle GetPanelHotZoneBounds(System.Drawing.Rectangle screenWorkingArea)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var topDip = IsFiniteNumber(Top) ? Top : _settings.PanelTop;
        var heightDip = GetPanelHeight();
        if (!IsFiniteNumber(topDip) || !IsUsableLength(heightDip))
        {
            return screenWorkingArea;
        }

        var top = (int)Math.Round(topDip * dpi.DpiScaleY);
        var height = Math.Max(1, (int)Math.Round(heightDip * dpi.DpiScaleY));
        top = Math.Clamp(top, screenWorkingArea.Top, screenWorkingArea.Bottom - 1);
        var bottom = Math.Clamp(top + height, top + 1, screenWorkingArea.Bottom);

        return new System.Drawing.Rectangle(
            screenWorkingArea.Left,
            top,
            screenWorkingArea.Width,
            bottom - top);
    }

    private void AnimateTo(double targetLeft, int durationMs, WindowAnimationEasing easing, Action? completed = null)
    {
        _windowAnimator.AnimateTo(targetLeft, durationMs, easing, completed);
    }

    private void AnimateOpacity(double targetOpacity, int durationMs)
    {
        BeginAnimation(OpacityProperty, null);
        var animation = new DoubleAnimation
        {
            To = targetOpacity,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };
        animation.Completed += (_, _) => Opacity = targetOpacity;
        BeginAnimation(OpacityProperty, animation);
    }

    private System.Windows.Point GetMouseScreenPointInDip(System.Windows.Input.MouseEventArgs e)
    {
        var screenPoint = PointToScreen(e.GetPosition(this));
        var dpi = VisualTreeHelper.GetDpi(this);
        return new System.Windows.Point(screenPoint.X / dpi.DpiScaleX, screenPoint.Y / dpi.DpiScaleY);
    }
}
