using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EdgePeek;

public partial class MainWindow
{
    private void DownloadsButton_Click(object sender, RoutedEventArgs e)
    {
        if (DownloadsPanel.Visibility == Visibility.Visible)
        {
            CloseDownloadsPanel();
            return;
        }

        TryCloseSettingsPage(() =>
        {
            OpenDownloadsPanel();
        });
    }

    private void CloseDownloadsButton_Click(object sender, RoutedEventArgs e)
    {
        CloseDownloadsPanel();
    }

    private void OpenDownloadsPanel()
    {
        SetTopBarVisible(true);
        SettingsHost.Visibility = Visibility.Collapsed;
        BrowserHost.Visibility = Visibility.Collapsed;
        DownloadsPanel.Visibility = Visibility.Visible;
        RenderDownloads();
    }

    private void CloseDownloadsPanel()
    {
        DownloadsPanel.Visibility = Visibility.Collapsed;
        if (SettingsHost.Visibility != Visibility.Visible)
        {
            BrowserHost.Visibility = Visibility.Visible;
        }
    }

    private void ClearDownloadsButton_Click(object sender, RoutedEventArgs e)
    {
        _downloadManager.ClearCompleted();
    }

    private void RenderDownloads()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(RenderDownloads);
            return;
        }

        DownloadsList.Items.Clear();
        if (_downloadManager.Records.Count == 0)
        {
            DownloadsList.Items.Add(new TextBlock
            {
                Text = "No downloads yet.",
                Foreground = (System.Windows.Media.Brush)FindResource("TextMuted"),
                Margin = new Thickness(4, 12, 4, 0)
            });
            return;
        }

        foreach (var record in _downloadManager.Records)
        {
            DownloadsList.Items.Add(CreateDownloadItem(record));
        }
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
}
