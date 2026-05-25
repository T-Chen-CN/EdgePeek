using System.IO;
using System.Text.Json;

namespace EdgePeek;

public sealed class DownloadHistoryStore
{
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public List<DownloadRecord> Load()
    {
        try
        {
            if (!File.Exists(AppPaths.DownloadsHistoryPath))
            {
                return [];
            }

            var json = File.ReadAllText(AppPaths.DownloadsHistoryPath);
            return JsonSerializer.Deserialize<List<DownloadRecord>>(json, _jsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            AppLog.Write("Download history load failed.");
            AppLog.Write(ex);
            return [];
        }
    }

    public void Save(IEnumerable<DownloadRecord> records)
    {
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.DataFolder);
                var json = JsonSerializer.Serialize(records, _jsonOptions);
                File.WriteAllText(AppPaths.DownloadsHistoryPath, json);
            }
            catch (Exception ex)
            {
                AppLog.Write("Download history save failed.");
                AppLog.Write(ex);
            }
        }
    }
}
