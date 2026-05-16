using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace EdgePeek;

public sealed class HotKeyManager : IDisposable
{
    private const int HotKeyId = 0x4550;
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private readonly Window _window;
    private readonly AppSettings _settings;
    private HwndSource? _source;
    private bool _isRegistered;
    private bool _shouldBeEnabled;

    public event EventHandler? Pressed;

    public HotKeyManager(Window window, AppSettings settings)
    {
        _window = window;
        _settings = settings;
        _window.SourceInitialized += Window_SourceInitialized;
        _window.Closed += (_, _) => Dispose();
    }

    public void SetEnabled(bool enabled)
    {
        _shouldBeEnabled = enabled;

        if (_source is null)
        {
            return;
        }

        if (enabled)
        {
            Register();
        }
        else
        {
            Unregister();
        }
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(_window);
        _source = HwndSource.FromHwnd(helper.Handle);
        _source?.AddHook(WndProc);
        if (_shouldBeEnabled)
        {
            Register();
        }
    }

    private void Register()
    {
        if (_source is null)
        {
            return;
        }

        Unregister();

        if (!TryParseGesture(_settings.HotkeyGesture, out var modifiers, out var key))
        {
            modifiers = ModControl | ModAlt;
            key = Key.Space;
        }

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        _isRegistered = RegisterHotKey(_source.Handle, HotKeyId, modifiers | ModNoRepeat, virtualKey);
        if (_isRegistered)
        {
            AppLog.Write($"Hotkey registered. gesture={_settings.HotkeyGesture}; modifiers={modifiers}; key={key}; vk={virtualKey}");
        }
        else
        {
            AppLog.Write($"Hotkey registration failed. gesture={_settings.HotkeyGesture}; modifiers={modifiers}; key={key}; vk={virtualKey}; error={Marshal.GetLastWin32Error()}");
        }
    }

    private void Unregister()
    {
        if (_source is null || !_isRegistered)
        {
            return;
        }

        UnregisterHotKey(_source.Handle, HotKeyId);
        _isRegistered = false;
        AppLog.Write("Hotkey unregistered.");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey && wParam.ToInt32() == HotKeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static bool TryParseGesture(string gesture, out uint modifiers, out Key key)
    {
        modifiers = 0;
        key = Key.None;

        if (string.IsNullOrWhiteSpace(gesture))
        {
            return false;
        }

        foreach (var part in gesture.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(part, "Ctrl", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(part, "Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModControl;
            }
            else if (string.Equals(part, "Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModAlt;
            }
            else if (string.Equals(part, "Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModShift;
            }
            else if (string.Equals(part, "Win", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= ModWin;
            }
            else if (Enum.TryParse(part, ignoreCase: true, out Key parsedKey))
            {
                key = parsedKey;
            }
        }

        return key != Key.None && modifiers != 0;
    }

    public void Dispose()
    {
        Unregister();
        _source?.RemoveHook(WndProc);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
