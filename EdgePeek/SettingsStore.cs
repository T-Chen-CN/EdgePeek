using System.IO;
using System.Text.Json;

namespace EdgePeek;

public sealed class SettingsStore
{
    private readonly string _settingsPath;
    private readonly string _settingsFolder;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public SettingsStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "EdgePeek");
        Directory.CreateDirectory(folder);
        _settingsFolder = folder;
        _settingsPath = Path.Combine(folder, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            AppLog.Write("Settings load failed; preserving corrupt settings file.");
            AppLog.Write(ex);
            TryBackupCorruptSettings();
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        var tempPath = Path.Combine(_settingsFolder, $"settings.{Environment.ProcessId}.tmp");
        File.WriteAllText(tempPath, json);

        if (File.Exists(_settingsPath))
        {
            File.Replace(tempPath, _settingsPath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, _settingsPath);
        }
    }

    private void TryBackupCorruptSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return;
            }

            var backupPath = Path.Combine(_settingsFolder, $"settings.corrupt-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.json");
            File.Copy(_settingsPath, backupPath, overwrite: false);
        }
        catch (Exception backupEx)
        {
            AppLog.Write("Failed to back up corrupt settings file.");
            AppLog.Write(backupEx);
        }
    }
}
