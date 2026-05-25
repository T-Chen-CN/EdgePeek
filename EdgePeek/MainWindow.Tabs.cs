using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Text.Json;
using EdgePeek.Services;

namespace EdgePeek;

public partial class MainWindow
{
    private const string DesktopViewModeIcon = "\uE977";
    private const string MobileViewModeIcon = "\uE8EA";
    private const string MobileUserAgent = "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Mobile Safari/537.36";
    private const int DefaultMobileWidth = 390;
    private const int DefaultMobileHeight = 844;

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
        if (_activeTab is null)
        {
            return;
        }

        var url = UrlNormalizer.NormalizeAddress(input);
        AddressBox.Text = url;
        NavigateTab(_activeTab, url);
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
            var mode = index < _settings.TabViewModes.Count ? _settings.TabViewModes[index] : BrowserViewMode.Desktop;
            AddTabAsync(urls[index], activate: index == activeIndex, mode).Forget("Restore tab");
        }
    }

    private Task<BrowserTab> AddTabAsync(string url, bool activate)
    {
        return AddTabAsync(url, activate, BrowserViewMode.Desktop);
    }

    private async Task<BrowserTab> AddTabAsync(string url, bool activate, BrowserViewMode mode)
    {
        var normalizedUrl = UrlNormalizer.NormalizeAddress(url);
        var tab = new BrowserTab(normalizedUrl)
        {
            Title = GetTitleFromUrl(normalizedUrl),
            ViewMode = mode
        };

        _tabs.Add(tab);
        ConfigureTab(tab);
        BrowserHost.Children.Add(tab.Browser);
        RenderTabs();

        try
        {
            await tab.Browser.EnsureCoreWebView2Async(await _webViewEnvironmentFactory.GetAsync());
            await ApplyViewModeAsync(tab);
            NavigateTab(tab, normalizedUrl);
        }
        catch (Exception ex)
        {
            AppLog.Write(ex);
            ShowWebView2StartupError(ex);
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
            tab.Browser.CoreWebView2.DownloadStarting += (_, downloadArgs) =>
            {
                if (_downloadManager.HandleDownloadStarting(downloadArgs, this))
                {
                    NotifyDownloadStarted();
                }
            };

            tab.Browser.CoreWebView2
                .AddScriptToExecuteOnDocumentCreatedAsync(BrowserEnhancementScriptProvider.Script)
                .Forget("Register browser enhancement script");
            ApplyViewModeAsync(tab).Forget("Apply view mode");
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

    private void ViewModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTab is null)
        {
            return;
        }

        TryCloseSettingsPage(() => ToggleActiveViewModeAsync().Forget("Toggle view mode"));
    }

    private async Task ToggleActiveViewModeAsync()
    {
        if (_activeTab is null)
        {
            return;
        }

        _activeTab.ViewMode = _activeTab.ViewMode == BrowserViewMode.Mobile
            ? BrowserViewMode.Desktop
            : BrowserViewMode.Mobile;
        UpdateViewModeButton();
        SaveTabs();

        await ApplyViewModeAsync(_activeTab);
        _activeTab.Browser.Reload();
    }

    private async Task ApplyViewModeAsync(BrowserTab tab)
    {
        if (tab.Browser.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            if (tab.ViewMode == BrowserViewMode.Mobile)
            {
                await tab.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync(
                    "Emulation.setUserAgentOverride",
                    GetMobileUserAgentOverrideJson());
                await ApplyMobileDeviceMetricsAsync(tab);
                await tab.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync(
                    "Emulation.setTouchEmulationEnabled",
                    """{ "enabled": true, "maxTouchPoints": 5 }""");
                await tab.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync(
                    "Emulation.setEmitTouchEventsForMouse",
                    """{ "enabled": true, "configuration": "mobile" }""");
            }
            else
            {
                await tab.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.clearDeviceMetricsOverride", "{}");
                await tab.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync(
                    "Emulation.setTouchEmulationEnabled",
                    """{ "enabled": false }""");
                await tab.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync(
                    "Emulation.setEmitTouchEventsForMouse",
                    """{ "enabled": false }""");
                await tab.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync(
                    "Emulation.setUserAgentOverride",
                    """{ "userAgent": "" }""");
            }
        }
        catch (Exception ex)
        {
            AppLog.Write("View mode emulation failed.");
            AppLog.Write(ex);
        }
    }

    private void ScheduleMobileMetricsUpdate()
    {
        if (_activeTab?.ViewMode != BrowserViewMode.Mobile)
        {
            return;
        }

        _mobileMetricsResizeTimer.Stop();
        _mobileMetricsResizeTimer.Start();
    }

    private async Task UpdateActiveMobileMetricsAsync()
    {
        if (_activeTab?.ViewMode != BrowserViewMode.Mobile)
        {
            return;
        }

        await ApplyMobileDeviceMetricsAsync(_activeTab);
    }

    private async Task ApplyMobileDeviceMetricsAsync(BrowserTab tab)
    {
        if (tab.Browser.CoreWebView2 is null)
        {
            return;
        }

        var width = GetCssPixelLength(BrowserHost.ActualWidth, DefaultMobileWidth);
        var height = GetCssPixelLength(BrowserHost.ActualHeight, DefaultMobileHeight);
        var json = JsonSerializer.Serialize(new
        {
            width,
            height,
            deviceScaleFactor = 3,
            mobile = true,
            screenOrientation = new
            {
                type = height >= width ? "portraitPrimary" : "landscapePrimary",
                angle = height >= width ? 0 : 90
            }
        });

        await tab.Browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride", json);
    }

    private static int GetCssPixelLength(double value, int fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 1)
        {
            return fallback;
        }

        return Math.Max(1, (int)Math.Round(value));
    }

    private static string GetMobileUserAgentOverrideJson()
    {
        return JsonSerializer.Serialize(new
        {
            userAgent = MobileUserAgent,
            platform = "Android",
            userAgentMetadata = new
            {
                brands = new[]
                {
                    new { brand = "Chromium", version = "125" },
                    new { brand = "Google Chrome", version = "125" },
                    new { brand = "Not.A/Brand", version = "24" }
                },
                fullVersionList = new[]
                {
                    new { brand = "Chromium", version = "125.0.0.0" },
                    new { brand = "Google Chrome", version = "125.0.0.0" },
                    new { brand = "Not.A/Brand", version = "24.0.0.0" }
                },
                platform = "Android",
                platformVersion = "14.0.0",
                architecture = "",
                model = "Pixel 8",
                mobile = true
            }
        });
    }

    private static void NavigateTab(BrowserTab tab, string url)
    {
        tab.Url = url;
        tab.Browser.Source = new Uri(url);
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
        UpdateViewModeButton();
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
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = true,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Stretch = Stretch.Uniform
                };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                RenderOptions.SetEdgeMode(image, EdgeMode.Aliased);
                tabContent = new Border
                {
                    Width = 30,
                    Height = 30,
                    CornerRadius = new CornerRadius(6),
                    Background = selected ? System.Windows.Media.Brushes.Transparent : (System.Windows.Media.Brush)FindResource("SurfaceSoft"),
                    BorderBrush = (System.Windows.Media.Brush)FindResource("BorderSoft"),
                    BorderThickness = new Thickness(1),
                    Child = image
                };
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
        _settings.TabViewModes = _tabs
            .Where(tab => !string.IsNullOrWhiteSpace(tab.Url))
            .Select(tab => tab.ViewMode)
            .ToList();
        _settings.ActiveTabIndex = _activeTab is null ? 0 : Math.Max(0, _tabs.IndexOf(_activeTab));
        _settings.LastUrl = _activeTab?.Url ?? _settings.LastUrl;
        _settingsStore.Save(_settings);
    }

    private void UpdateViewModeButton()
    {
        var mobile = _activeTab?.ViewMode == BrowserViewMode.Mobile;
        ViewModeIcon.Text = mobile ? MobileViewModeIcon : DesktopViewModeIcon;
        ViewModeButton.ToolTip = mobile ? "Mobile mode" : "Desktop mode";
        ViewModeButton.Background = mobile
            ? (System.Windows.Media.Brush)FindResource("AccentSoft")
            : System.Windows.Media.Brushes.Transparent;
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

}
