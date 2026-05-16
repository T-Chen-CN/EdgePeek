using System.IO;

namespace EdgePeek;

public static class AppLog
{
    private static readonly object Gate = new();

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
            File.AppendAllText(LogPath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {message}{Environment.NewLine}");
        }
    }

    public static void Write(Exception exception)
    {
        Write(exception.ToString());
    }
}
