using System.Drawing;
using System.Windows.Forms;
using EdgePeek.Localization;

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
        var zh = Strings.IsChinese(_settings.Language);
        var menu = new ContextMenuStrip();
        menu.Items.Add(Strings.Show(zh), null, (_, _) => _show());
        menu.Items.Add(Strings.Hide(zh), null, (_, _) => _hide());
        menu.Items.Add(Strings.SettingsTitle(zh), null, (_, _) => _settingsAction());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.Exit(zh), null, (_, _) => _exit());

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
