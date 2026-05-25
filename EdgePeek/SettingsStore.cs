using System.IO;
using System.Text.Json;

namespace EdgePeek;

public sealed class SettingsStore
{
    private readonly object _saveGate = new();
    private readonly string _settingsPath;
    private readonly string _settingsFolder;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public SettingsStore()
        : this(AppPaths.DataFolder, migrateLegacySettings: true)
    {
    }

    public SettingsStore(string settingsFolder)
        : this(settingsFolder, migrateLegacySettings: false)
    {
    }

    private SettingsStore(string settingsFolder, bool migrateLegacySettings)
    {
        Directory.CreateDirectory(settingsFolder);
        _settingsFolder = settingsFolder;
        _settingsPath = Path.Combine(settingsFolder, "settings.json");
        if (migrateLegacySettings)
        {
            TryMigrateLegacySettings();
        }
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

    public bool Save(AppSettings settings)
    {
        lock (_saveGate)
        {
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            var tempPath = Path.Combine(_settingsFolder, $"settings.{Environment.ProcessId}.tmp");

            try
            {
                File.WriteAllText(tempPath, json);

                if (File.Exists(_settingsPath))
                {
                    File.Replace(tempPath, _settingsPath, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(tempPath, _settingsPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                AppLog.Write("Settings save failed.");
                AppLog.Write(ex);
                TryDeleteTempFile(tempPath);
                return false;
            }
        }
    }

    private static void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            AppLog.Write("Failed to delete temporary settings file.");
            AppLog.Write(ex);
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

    private void TryMigrateLegacySettings()
    {
        try
        {
            if (File.Exists(_settingsPath) || !File.Exists(AppPaths.LegacySettingsPath))
            {
                return;
            }

            File.Copy(AppPaths.LegacySettingsPath, _settingsPath, overwrite: false);
            AppLog.Write($"Migrated settings from {AppPaths.LegacySettingsPath} to {_settingsPath}.");
        }
        catch (Exception ex)
        {
            AppLog.Write("Failed to migrate legacy settings.");
            AppLog.Write(ex);
        }
    }
}
