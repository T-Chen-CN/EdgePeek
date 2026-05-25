namespace EdgePeek;

public enum DownloadRecordState
{
    InProgress,
    Completed,
    Canceled,
    Failed
}

public sealed class DownloadRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long ReceivedBytes { get; set; }
    public DownloadRecordState State { get; set; } = DownloadRecordState.InProgress;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? CompletedAt { get; set; }
    public string Error { get; set; } = string.Empty;
}
