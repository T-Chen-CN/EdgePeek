using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Forms = System.Windows.Forms;

namespace EdgePeek;

public partial class MainWindow
{
    private void TopResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginNativeResize(WmszTop, e);
    }

    private void BottomResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginNativeResize(WmszBottom, e);
    }

    private void BeginNativeResize(int edge, MouseButtonEventArgs e)
    {
        if (ResizeMode == ResizeMode.NoResize || WindowState == WindowState.Maximized)
        {
            return;
        }

        e.Handled = true;
        _hideDelayTimer.Stop();
        _autoHideCheckTimer.Stop();
        _hotEdgeWatcher.Pause();
        _windowAnimator.Stop();

        var helper = new WindowInteropHelper(this);
        SendMessage(helper.Handle, WmSysCommand, (IntPtr)(ScSize + edge), IntPtr.Zero);

        _settings.PanelHeight = ClampPanelHeight(Height);
        _settings.PanelTop = ClampPanelTop(Top, Height);
        _settingsStore.Save(_settings);
        if (_isShown)
        {
            _autoHideCheckTimer.Start();
        }
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

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
