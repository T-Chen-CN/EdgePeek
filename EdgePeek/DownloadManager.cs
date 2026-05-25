using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace EdgePeek;

public sealed class DownloadManager
{
    private readonly AppSettings _settings;
    private readonly DownloadHistoryStore _store = new();
    private readonly List<DownloadRecord> _records;
    private readonly Dictionary<Guid, CoreWebView2DownloadOperation> _activeOperations = [];

    public IReadOnlyList<DownloadRecord> Records => _records;

    public event EventHandler? RecordsChanged;

    public DownloadManager(AppSettings settings)
    {
        _settings = settings;
        _records = _store.Load();
        foreach (var record in _records.Where(item => item.State == DownloadRecordState.InProgress))
        {
            record.State = DownloadRecordState.Canceled;
            record.Error = "Download interrupted.";
            record.CompletedAt = DateTimeOffset.Now;
        }
    }

    public static string GetEffectiveDownloadFolder(AppSettings settings)
    {
        return string.IsNullOrWhiteSpace(settings.DownloadFolder)
            ? AppPaths.DefaultDownloadFolder
            : Environment.ExpandEnvironmentVariables(settings.DownloadFolder);
    }

    public bool HandleDownloadStarting(CoreWebView2DownloadStartingEventArgs args, Window owner)
    {
        var operation = args.DownloadOperation;
        var targetPath = ChooseTargetPath(args, owner);
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            AppLog.Write($"Download canceled before start. uri={operation.Uri}");
            args.Cancel = true;
            return false;
        }

        args.ResultFilePath = targetPath;
        args.Handled = true;
        AppLog.Write($"Download starting. uri={operation.Uri}; target={targetPath}");

        var record = new DownloadRecord
        {
            FileName = Path.GetFileName(targetPath),
            FilePath = targetPath,
            SourceUrl = operation.Uri,
            TotalBytes = ToSignedBytes(operation.TotalBytesToReceive),
            ReceivedBytes = operation.BytesReceived
        };

        _records.Insert(0, record);
        _activeOperations[record.Id] = operation;
        SaveAndNotify();

        operation.BytesReceivedChanged += (_, _) =>
        {
            record.ReceivedBytes = operation.BytesReceived;
            record.TotalBytes = ToSignedBytes(operation.TotalBytesToReceive);
            SaveAndNotify();
        };

        operation.StateChanged += (_, _) =>
        {
            record.ReceivedBytes = operation.BytesReceived;
            record.TotalBytes = ToSignedBytes(operation.TotalBytesToReceive);
            record.CompletedAt = DateTimeOffset.Now;
            record.State = operation.State switch
            {
                CoreWebView2DownloadState.Completed => DownloadRecordState.Completed,
                CoreWebView2DownloadState.Interrupted => DownloadRecordState.Failed,
                _ => DownloadRecordState.Canceled
            };
            if (record.State == DownloadRecordState.Failed)
            {
                record.Error = operation.InterruptReason.ToString();
            }
            AppLog.Write($"Download state changed. file={record.FilePath}; state={record.State}; bytes={record.ReceivedBytes}/{record.TotalBytes}; error={record.Error}");
            _activeOperations.Remove(record.Id);

            SaveAndNotify();
        };

        return true;
    }

    public void Remove(DownloadRecord record)
    {
        _records.Remove(record);
        SaveAndNotify();
    }

    public void ClearCompleted()
    {
        _records.RemoveAll(record => record.State != DownloadRecordState.InProgress);
        SaveAndNotify();
    }

    public void Cancel(DownloadRecord record)
    {
        if (_activeOperations.TryGetValue(record.Id, out var operation))
        {
            operation.Cancel();
            AppLog.Write($"Download cancel requested. file={record.FilePath}");
        }

        record.State = DownloadRecordState.Canceled;
        record.CompletedAt = DateTimeOffset.Now;
        _activeOperations.Remove(record.Id);
        SaveAndNotify();
    }

    private string? ChooseTargetPath(CoreWebView2DownloadStartingEventArgs args, Window owner)
    {
        var suggestedName = SanitizeFileName(Path.GetFileName(args.ResultFilePath));
        if (string.IsNullOrWhiteSpace(suggestedName))
        {
            suggestedName = "download";
        }

        if (_settings.AskWhereToSaveDownloads)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = suggestedName,
                InitialDirectory = EnsureDownloadFolder(),
                OverwritePrompt = true
            };
            return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
        }

        var folder = EnsureDownloadFolder();
        return GetUniquePath(Path.Combine(folder, suggestedName));
    }

    private string EnsureDownloadFolder()
    {
        var folder = GetEffectiveDownloadFolder(_settings);
        try
        {
            Directory.CreateDirectory(folder);
            return folder;
        }
        catch (Exception ex)
        {
            AppLog.Write("Download folder could not be created; falling back to temp.");
            AppLog.Write(ex);
            var fallback = Path.Combine(Path.GetTempPath(), "EdgePeek", "Downloads");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var folder = Path.GetDirectoryName(path) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var index = 1; index < 10000; index++)
        {
            var candidate = Path.Combine(folder, $"{name} ({index}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(folder, $"{name}-{Guid.NewGuid():N}{extension}");
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalid, '_');
        }

        return fileName.Trim();
    }

    private static long ToSignedBytes(ulong? bytes)
    {
        if (bytes is null)
        {
            return 0;
        }

        return bytes.Value > long.MaxValue ? long.MaxValue : (long)bytes.Value;
    }

    private void SaveAndNotify()
    {
        _store.Save(_records);
        RecordsChanged?.Invoke(this, EventArgs.Empty);
    }
}
