using System.Drawing;
using System.Windows.Forms;

namespace EdgePeek;

public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly AppSettings _settings;
    private readonly Action _show;
    private readonly Action _hide;
    private readonly Action _settingsAction;
    private readonly Action _exit;

    public TrayIconManager(AppSettings appSettings, Action show, Action hide, Action settings, Action exit)
    {
        _settings = appSettings;
        _show = show;
        _hide = hide;
        _settingsAction = settings;
        _exit = exit;

        _notifyIcon = new NotifyIcon
        {
            Text = "EdgePeek",
            Icon = SystemIcons.Application,
            Visible = true
        };
        RefreshMenu();

        _notifyIcon.DoubleClick += (_, _) => show();
    }

    public void RefreshMenu()
    {
        var zh = string.Equals(_settings.Language, "zh-CN", StringComparison.OrdinalIgnoreCase);
        var menu = new ContextMenuStrip();
        menu.Items.Add(zh ? "显示" : "Show", null, (_, _) => _show());
        menu.Items.Add(zh ? "隐藏" : "Hide", null, (_, _) => _hide());
        menu.Items.Add(zh ? "设置" : "Settings", null, (_, _) => _settingsAction());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(zh ? "退出" : "Exit", null, (_, _) => _exit());

        var oldMenu = _notifyIcon.ContextMenuStrip;
        _notifyIcon.ContextMenuStrip = menu;
        oldMenu?.Dispose();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
