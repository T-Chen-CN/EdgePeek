using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using EdgePeek.Localization;
using EdgePeek.Services;
using Forms = System.Windows.Forms;

namespace EdgePeek;

public partial class MainWindow : Window
{
    private const int WmSysCommand = 0x0112;
    private const int ScSize = 0xF000;
    private const int WmszTop = 3;
    private const int WmszBottom = 6;
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
            ShowWebView2StartupError(ex);
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

    private void ShowWebView2StartupError(Exception exception)
    {
        var message = "Microsoft Edge WebView2 Runtime is required to run EdgePeek. Please install WebView2 Runtime and restart EdgePeek.";
        if (!string.IsNullOrWhiteSpace(exception.Message))
        {
            message += $"{Environment.NewLine}{Environment.NewLine}Details: {exception.Message}";
        }

        System.Windows.MessageBox.Show(this, message, "WebView2 Runtime required", MessageBoxButton.OK, MessageBoxImage.Error);
    }

}
