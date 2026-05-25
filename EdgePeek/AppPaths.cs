using System.IO;
using System.Runtime.InteropServices;

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
        GetSystemDownloadsFolder(),
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

    private static string GetSystemDownloadsFolder()
    {
        var downloadsId = new Guid("374DE290-123F-4565-9164-39C4925E467B");
        var result = SHGetKnownFolderPath(ref downloadsId, 0, IntPtr.Zero, out var pathPointer);
        try
        {
            if (result == 0 && pathPointer != IntPtr.Zero)
            {
                var path = Marshal.PtrToStringUni(pathPointer);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }
        }
        finally
        {
            if (pathPointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(pathPointer);
            }
        }

        AppLog.Write($"System Downloads folder lookup failed. HRESULT=0x{result:X8}");
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(
        ref Guid rfid,
        uint dwFlags,
        IntPtr hToken,
        out IntPtr ppszPath);
}
