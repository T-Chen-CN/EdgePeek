using System.IO;

namespace EdgePeek;

public static class AppPaths
{
    private const string AppFolderName = "EdgePeek";
    private const string DataFolderName = "Data";
    private static readonly Lazy<string> DataFolderLazy = new(ResolveDataFolder);

    public static string DataFolder => DataFolderLazy.Value;

    public static string SettingsPath => Path.Combine(DataFolder, "settings.json");

    public static string LogPath => Path.Combine(DataFolder, "edgepeek.log");

    public static string WebView2Folder => Path.Combine(DataFolder, "WebView2");

    public static string DownloadsHistoryPath => Path.Combine(DataFolder, "downloads.json");

    public static string DefaultDownloadFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads",
        AppFolderName);

    public static string LegacySettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppFolderName,
        "settings.json");

    public static string FallbackDataFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName,
        DataFolderName);

    private static string ResolveDataFolder()
    {
        var appDataFolder = Path.Combine(AppContext.BaseDirectory, DataFolderName);
        if (TryPrepareWritableFolder(appDataFolder))
        {
            return appDataFolder;
        }

        Directory.CreateDirectory(FallbackDataFolder);
        return FallbackDataFolder;
    }

    private static bool TryPrepareWritableFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var probePath = Path.Combine(folder, $".write-test-{Environment.ProcessId}.tmp");
            File.WriteAllText(probePath, string.Empty);
            File.Delete(probePath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
