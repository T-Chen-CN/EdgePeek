using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EdgePeek;

public sealed class GlobalMouseHook : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmLButtonDown = 0x0201;
    private const int WmRButtonDown = 0x0204;
    private const int WmMButtonDown = 0x0207;

    private readonly LowLevelMouseProc _proc;
    private IntPtr _hookId;

    public event EventHandler? MouseDown;

    public GlobalMouseHook()
    {
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero)
        {
            return;
        }

        _hookId = SetWindowsHookEx(WhMouseLl, _proc, IntPtr.Zero, 0);
    }

    public void Stop()
    {
        if (_hookId == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            if (message is WmLButtonDown or WmRButtonDown or WmMButtonDown)
            {
                MouseDown?.Invoke(this, EventArgs.Empty);
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
}
