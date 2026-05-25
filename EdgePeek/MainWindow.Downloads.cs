using System.Windows;

namespace EdgePeek;

public partial class MainWindow
{
    private readonly HashSet<Guid> _knownTerminalDownloadIds = [];
    private DownloadsPopupWindow? _downloadsPopup;
    private bool _hasUnreadDownloadResult;

    private void DownloadsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_downloadsPopup?.IsVisible == true)
        {
            _downloadsPopup.ClosePopup();
            return;
        }

        TryCloseSettingsPage(ShowDownloadsPopup);
    }

    private void NotifyDownloadStarted()
    {
        _hasUnreadDownloadResult = false;
        ShowDownloadsPopup();
        UpdateDownloadButtonState();
    }

    private void RefreshDownloads()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(RefreshDownloads);
            return;
        }

        var hasNewTerminalRecord = false;
        foreach (var record in _downloadManager.Records.Where(IsTerminalDownload))
        {
            if (_knownTerminalDownloadIds.Add(record.Id))
            {
                hasNewTerminalRecord = true;
            }
        }

        if (hasNewTerminalRecord && _downloadsPopup?.IsVisible != true)
        {
            _hasUnreadDownloadResult = true;
        }

        _downloadsPopup?.Refresh();
        UpdateDownloadButtonState();
    }

    private void ShowDownloadsPopup()
    {
        EnsureDownloadsPopup();
        var popup = _downloadsPopup;
        if (popup is null)
        {
            return;
        }

        _hasUnreadDownloadResult = false;
        popup.ShowNear(this, DownloadsButton);
        UpdateDownloadButtonState();
    }

    private void CloseDownloadsPopup()
    {
        _downloadsPopup?.ClosePopup();
    }

    private void CloseDownloadsPopupForShutdown()
    {
        _downloadsPopup?.CloseForOwnerShutdown();
        _downloadsPopup = null;
    }

    private void CloseDownloadsPopupFromExternalClick()
    {
        if (_downloadsPopup?.IsVisible != true)
        {
            return;
        }

        var cursor = System.Windows.Forms.Cursor.Position;
        if (_downloadsPopup.ContainsScreenPoint(cursor) || IsScreenPointInsideElement(DownloadsButton, cursor))
        {
            return;
        }

        _downloadsPopup.ClosePopup();
    }

    private void EnsureDownloadsPopup()
    {
        if (_downloadsPopup is not null)
        {
            return;
        }

        _downloadsPopup = new DownloadsPopupWindow(_downloadManager);
    }

    private void UpdateDownloadButtonState()
    {
        if (_downloadManager.Records.Any(record => record.State == DownloadRecordState.InProgress))
        {
            DownloadsIcon.Text = "\uE896";
            DownloadsIcon.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
            DownloadsButton.ToolTip = "Downloads in progress";
            return;
        }

        if (_hasUnreadDownloadResult)
        {
            DownloadsIcon.Text = "\uE930";
            DownloadsIcon.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
            DownloadsButton.ToolTip = "Download completed";
            return;
        }

        DownloadsIcon.Text = "\uE896";
        DownloadsIcon.Foreground = (System.Windows.Media.Brush)FindResource("TextMain");
        DownloadsButton.ToolTip = "Downloads";
    }

    private static bool IsTerminalDownload(DownloadRecord record)
    {
        return record.State is DownloadRecordState.Completed or DownloadRecordState.Canceled or DownloadRecordState.Failed;
    }

    private static bool IsScreenPointInsideElement(FrameworkElement element, System.Drawing.Point point)
    {
        var localPoint = element.PointFromScreen(new System.Windows.Point(point.X, point.Y));
        return localPoint.X >= 0 &&
               localPoint.Y >= 0 &&
               localPoint.X <= element.ActualWidth &&
               localPoint.Y <= element.ActualHeight;
    }
}
