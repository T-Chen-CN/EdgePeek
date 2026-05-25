using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace EdgePeek;

public partial class DownloadsPopupWindow : Window
{
    private const double ScreenPadding = 8;
    private const double PlacementGap = 8;
    private readonly DownloadManager _downloadManager;
    private bool _allowClose;

    public DownloadsPopupWindow(DownloadManager downloadManager)
    {
        _downloadManager = downloadManager;
        InitializeComponent();
        Deactivated += (_, _) => Hide();
    }

    public void ShowNear(Window owner, FrameworkElement placementTarget)
    {
        Owner = owner;
        Refresh();
        Show();
        PositionNear(placementTarget);
    }

    public void Refresh()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(Refresh);
            return;
        }

        DownloadsList.Items.Clear();
        if (_downloadManager.Records.Count == 0)
        {
            DownloadsList.Items.Add(new TextBlock
            {
                Text = "No downloads yet.",
                Foreground = (System.Windows.Media.Brush)FindResource("TextMuted"),
                Margin = new Thickness(4, 12, 4, 6)
            });
            return;
        }

        foreach (var record in _downloadManager.Records)
        {
            DownloadsList.Items.Add(CreateDownloadItem(record));
        }
    }

    public void ClosePopup()
    {
        Hide();
    }

    public bool ContainsScreenPoint(System.Drawing.Point point)
    {
        if (!IsVisible)
        {
            return false;
        }

        var localPoint = PointFromScreen(new System.Windows.Point(point.X, point.Y));
        return localPoint.X >= 0 &&
               localPoint.Y >= 0 &&
               localPoint.X <= ActualWidth &&
               localPoint.Y <= ActualHeight;
    }

    public void CloseForOwnerShutdown()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void PositionNear(FrameworkElement placementTarget)
    {
        UpdateLayout();
        var source = PresentationSource.FromVisual(placementTarget);
        var fromDevice = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
        var topLeft = fromDevice.Transform(placementTarget.PointToScreen(new System.Windows.Point(0, 0)));
        var bottomRight = fromDevice.Transform(placementTarget.PointToScreen(new System.Windows.Point(placementTarget.ActualWidth, placementTarget.ActualHeight)));
        var workArea = SystemParameters.WorkArea;
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : 240;

        var left = bottomRight.X - width;
        var top = bottomRight.Y + PlacementGap;
        if (top + height > workArea.Bottom - ScreenPadding)
        {
            top = topLeft.Y - height - PlacementGap;
        }

        Left = Clamp(left, workArea.Left + ScreenPadding, workArea.Right - width - ScreenPadding);
        Top = Clamp(top, workArea.Top + ScreenPadding, workArea.Bottom - height - ScreenPadding);
    }

    private FrameworkElement CreateDownloadItem(DownloadRecord record)
    {
        var root = new Border
        {
            Background = (System.Windows.Media.Brush)FindResource("SurfaceSoft"),
            BorderBrush = (System.Windows.Media.Brush)FindResource("BorderSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var stack = new StackPanel();
        root.Child = stack;

        stack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(record.FileName) ? "Download" : record.FileName,
            Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        stack.Children.Add(new TextBlock
        {
            Text = GetDownloadStatus(record),
            Foreground = (System.Windows.Media.Brush)FindResource("TextMuted"),
            FontSize = 12,
            Margin = new Thickness(0, 3, 0, 6),
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        if (record.State == DownloadRecordState.InProgress)
        {
            stack.Children.Add(new System.Windows.Controls.ProgressBar
            {
                Minimum = 0,
                Maximum = record.TotalBytes > 0 ? record.TotalBytes : 100,
                Value = record.TotalBytes > 0 ? record.ReceivedBytes : 0,
                IsIndeterminate = record.TotalBytes <= 0,
                Height = 4,
                Margin = new Thickness(0, 0, 0, 8)
            });
        }

        var actions = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        if (record.State == DownloadRecordState.Completed)
        {
            actions.Children.Add(CreateSmallButton("Open", () => OpenFile(record.FilePath)));
            actions.Children.Add(CreateSmallButton("Folder", () => OpenFolder(record.FilePath)));
        }
        else if (record.State == DownloadRecordState.InProgress)
        {
            actions.Children.Add(CreateSmallButton("Cancel", () => _downloadManager.Cancel(record)));
        }

        actions.Children.Add(CreateSmallButton("Remove", () => _downloadManager.Remove(record)));
        stack.Children.Add(actions);

        return root;
    }

    private System.Windows.Controls.Button CreateSmallButton(string text, Action action)
    {
        var button = new System.Windows.Controls.Button
        {
            Content = text,
            MinHeight = 24,
            Padding = new Thickness(7, 1, 7, 1),
            Margin = new Thickness(6, 0, 0, 0)
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void ClearDownloadsButton_Click(object sender, RoutedEventArgs e)
    {
        _downloadManager.ClearCompleted();
    }

    private void CloseDownloadsButton_Click(object sender, RoutedEventArgs e)
    {
        ClosePopup();
    }

    private static string GetDownloadStatus(DownloadRecord record)
    {
        var size = record.TotalBytes > 0
            ? $"{FormatBytes(record.ReceivedBytes)} / {FormatBytes(record.TotalBytes)}"
            : FormatBytes(record.ReceivedBytes);
        return record.State switch
        {
            DownloadRecordState.InProgress => $"Downloading {size}",
            DownloadRecordState.Completed => $"Completed {FormatBytes(record.ReceivedBytes)}",
            DownloadRecordState.Canceled => "Canceled",
            DownloadRecordState.Failed => string.IsNullOrWhiteSpace(record.Error) ? "Failed" : $"Failed: {record.Error}",
            _ => size
        };
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = Math.Max(0, bytes);
        var unit = 0;
        var display = (double)value;
        while (display >= 1024 && unit < units.Length - 1)
        {
            display /= 1024;
            unit++;
        }

        return $"{display.ToString(unit == 0 ? "0" : "0.0", CultureInfo.InvariantCulture)} {units[unit]}";
    }

    private static void OpenFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true
        });
    }

    private static void OpenFolder(string path)
    {
        var folder = File.Exists(path) ? Path.GetDirectoryName(path) : path;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(folder)
        {
            UseShellExecute = true
        });
    }

    private static double Clamp(double value, double min, double max)
    {
        if (max < min)
        {
            return min;
        }

        return Math.Min(Math.Max(value, min), max);
    }
}
