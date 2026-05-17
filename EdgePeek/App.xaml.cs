namespace EdgePeek;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private TrayIconManager? _trayIconManager;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        AppLog.Write("Application starting.");

        var settingsStore = new SettingsStore();
        var settings = settingsStore.Load();

        _mainWindow = new MainWindow(settings, settingsStore);
        _trayIconManager = new TrayIconManager(
            appSettings: settings,
            show: () => _mainWindow.ShowPanel(forceFocus: true),
            hide: () => _mainWindow.HidePanel(),
            settings: () => _mainWindow.OpenSettings(),
            exit: ExitApplication);
        _mainWindow.SettingsApplied += (_, _) => _trayIconManager.RefreshMenu();

        _mainWindow.Show();
        if (ShouldShowOnStartup(e.Args))
        {
            _mainWindow.Dispatcher.BeginInvoke(() => _mainWindow.ShowPanel(forceFocus: true));
        }
    }

    private static bool ShouldShowOnStartup(string[] args)
    {
        return args.Any(arg =>
            string.Equals(arg, "--show", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "/show", StringComparison.OrdinalIgnoreCase));
    }

    private void ExitApplication()
    {
        AppLog.Write("Application exiting.");
        _mainWindow?.CloseForExit();
        Shutdown();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _trayIconManager?.Dispose();
        base.OnExit(e);
    }
}
