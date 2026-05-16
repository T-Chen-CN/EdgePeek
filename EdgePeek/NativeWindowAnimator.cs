using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace EdgePeek;

public enum WindowAnimationEasing
{
    EaseOutCubic,
    EaseInCubic
}

public sealed class NativeWindowAnimator
{
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    private readonly Window _window;
    private readonly Stopwatch _stopwatch = new();
    private double _fromLeft;
    private double _toLeft;
    private int _durationMs;
    private WindowAnimationEasing _easing;
    private Action? _completed;
    private bool _isRunning;

    public NativeWindowAnimator(Window window)
    {
        _window = window;
    }

    public void AnimateTo(double targetLeft, int durationMs, WindowAnimationEasing easing, Action? completed = null)
    {
        Stop();

        _fromLeft = _window.Left;
        _toLeft = targetLeft;
        _durationMs = Math.Max(1, durationMs);
        _easing = easing;
        _completed = completed;
        _stopwatch.Restart();
        _isRunning = true;
        CompositionTarget.Rendering += CompositionTarget_Rendering;
    }

    public void Stop()
    {
        if (_isRunning)
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            _isRunning = false;
        }
        _stopwatch.Reset();
    }

    private void CompositionTarget_Rendering(object? sender, EventArgs e)
    {
        var progress = Math.Clamp(_stopwatch.Elapsed.TotalMilliseconds / _durationMs, 0, 1);
        var eased = _easing == WindowAnimationEasing.EaseInCubic ? EaseInCubic(progress) : EaseOutCubic(progress);
        var nextLeft = _fromLeft + ((_toLeft - _fromLeft) * eased);
        MoveTo(nextLeft);

        if (progress >= 1)
        {
            Stop();
            _window.Left = _toLeft;
            _completed?.Invoke();
            _completed = null;
        }
    }

    private void MoveTo(double left)
    {
        var helper = new WindowInteropHelper(_window);
        if (helper.Handle == IntPtr.Zero)
        {
            _window.Left = left;
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(_window);
        var x = (int)Math.Round(left * dpi.DpiScaleX);
        var y = (int)Math.Round(_window.Top * dpi.DpiScaleY);
        var width = (int)Math.Round(_window.ActualWidth * dpi.DpiScaleX);
        var height = (int)Math.Round(_window.ActualHeight * dpi.DpiScaleY);

        if (width <= 0 || height <= 0)
        {
            _window.Left = left;
            return;
        }

        SetWindowPos(helper.Handle, IntPtr.Zero, x, y, width, height, SwpNoZOrder | SwpNoActivate);
    }

    private static double EaseOutCubic(double value)
    {
        var t = 1 - value;
        return 1 - (t * t * t);
    }

    private static double EaseInCubic(double value)
    {
        return value * value * value;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
