using System.IO;

namespace EdgePeek;

public static class AppLog
{
    private static readonly object Gate = new();
    private const long MaxLogBytes = 512 * 1024;

    public static string LogPath
    {
        get
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EdgePeek");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "edgepeek.log");
        }
    }

    public static void Write(string message)
    {
        lock (Gate)
        {
            try
            {
                RotateIfNeeded();
                File.AppendAllText(LogPath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {message}{Environment.NewLine}");
            }
            catch
            {
                // Logging should never break the app's main flow.
            }
        }
    }

    public static void Write(Exception exception)
    {
        Write(exception.ToString());
    }

    private static void RotateIfNeeded()
    {
        try
        {
            var path = LogPath;
            var file = new FileInfo(path);
            if (!file.Exists || file.Length <= MaxLogBytes)
            {
                return;
            }

            var backupPath = Path.Combine(file.DirectoryName ?? string.Empty, "edgepeek.previous.log");
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            File.Move(path, backupPath);
        }
        catch
        {
        }
    }
}
