using System.Drawing;
using System.Windows.Forms;
using Timer = System.Windows.Threading.DispatcherTimer;

namespace EdgePeek;

public sealed class HotEdgeWatcher
{
    private readonly AppSettings _settings;
    private readonly Func<Rectangle, Rectangle>? _hotZoneBoundsProvider;
    private readonly Timer _timer;
    private bool _isPaused;
    private DateTimeOffset? _hotZoneEnteredAt;

    public event EventHandler? HotEdgeReached;

    public HotEdgeWatcher(AppSettings settings, Func<Rectangle, Rectangle>? hotZoneBoundsProvider = null)
    {
        _settings = settings;
        _hotZoneBoundsProvider = hotZoneBoundsProvider;
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
        _hotZoneEnteredAt = null;
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
        var bounds = screen.WorkingArea;
        var hotZoneBounds = _hotZoneBoundsProvider?.Invoke(bounds) ?? bounds;

        if (!IsInHotZone(cursor, bounds, hotZoneBounds, _settings.Edge, _settings.TriggerThickness))
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

    public static bool IsInHotZone(Point cursor, Rectangle screenBounds, Rectangle hotZoneBounds, DockEdge edge, int triggerThickness)
    {
        var top = Math.Max(screenBounds.Top, hotZoneBounds.Top);
        var bottom = Math.Min(screenBounds.Bottom, hotZoneBounds.Bottom);
        if (bottom <= top || cursor.Y < top || cursor.Y >= bottom)
        {
            return false;
        }

        return edge switch
        {
            DockEdge.Left => cursor.X <= screenBounds.Left + triggerThickness,
            DockEdge.Right => cursor.X >= screenBounds.Right - triggerThickness,
            _ => false
        };
    }
}
