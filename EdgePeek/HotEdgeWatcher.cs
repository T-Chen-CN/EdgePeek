using System.Drawing;
using System.Windows.Forms;
using Timer = System.Windows.Threading.DispatcherTimer;

namespace EdgePeek;

public sealed class HotEdgeWatcher
{
    private readonly AppSettings _settings;
    private readonly Timer _timer;
    private bool _isPaused;
    private DateTimeOffset? _hotZoneEnteredAt;

    public event EventHandler? HotEdgeReached;

    public HotEdgeWatcher(AppSettings settings)
    {
        _settings = settings;
        _timer = new Timer
        {
            Interval = TimeSpan.FromMilliseconds(settings.TriggerPollingMs)
        };
        _timer.Tick += (_, _) => CheckCursor();
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    public void Pause() => _isPaused = true;

    public void Resume() => _isPaused = false;

    public void Reconfigure()
    {
        _timer.Interval = TimeSpan.FromMilliseconds(_settings.TriggerPollingMs);
    }

    private void CheckCursor()
    {
        if (_isPaused)
        {
            _hotZoneEnteredAt = null;
            return;
        }

        var cursor = Cursor.Position;
        var screen = Screen.FromPoint(cursor);
        var bounds = screen.Bounds;

        if (!IsInHotZone(cursor, bounds))
        {
            _hotZoneEnteredAt = null;
            return;
        }

        _hotZoneEnteredAt ??= DateTimeOffset.Now;
        if (DateTimeOffset.Now - _hotZoneEnteredAt.Value >= TimeSpan.FromMilliseconds(_settings.EdgeHoverDelayMs))
        {
            _hotZoneEnteredAt = null;
            HotEdgeReached?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool IsInHotZone(Point cursor, Rectangle bounds)
    {
        return _settings.Edge switch
        {
            DockEdge.Left => cursor.X <= bounds.Left + _settings.TriggerThickness,
            DockEdge.Right => cursor.X >= bounds.Right - _settings.TriggerThickness,
            _ => false
        };
    }
}
