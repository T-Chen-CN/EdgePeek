using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace EdgePeek;

public static class WindowChromeHelper
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExAppWindow = 0x00040000;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

    public static void HideFromAltTab(Window window)
    {
        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero)
        {
            return;
        }

        var extendedStyle = GetWindowLong(helper.Handle, GwlExStyle);
        extendedStyle |= WsExToolWindow;
        extendedStyle &= ~WsExAppWindow;
        SetWindowLong(helper.Handle, GwlExStyle, extendedStyle);

        var cornerPreference = DwmwcpRound;
        _ = DwmSetWindowAttribute(helper.Handle, DwmwaWindowCornerPreference, ref cornerPreference, sizeof(int));
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
}
