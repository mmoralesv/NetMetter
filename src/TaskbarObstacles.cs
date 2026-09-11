using System.Windows.Automation;
using static NetMetter.NativeMethods;

namespace NetMetter;

/// <summary>
/// Periodically asks UI Automation where the taskbar's own buttons are (task list, Widgets,
/// tray icons, clock...). Windows 11 doesn't expose these as HWNDs, and some of them (e.g. the
/// Widgets button on a left-aligned taskbar) sit outside the notification area, so this is the
/// only reliable way for the automatic placement to find free space.
/// Scans run on a thread-pool thread; UI Automation calls into Explorer can be slow.
/// </summary>
internal sealed class TaskbarObstacles : IDisposable
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(5);

    private readonly System.Threading.Timer _timer;
    private IReadOnlyList<Rectangle> _current = [];
    private int _scanning;

    /// <summary>Raised on a background thread when the set of button rectangles changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Scanning is skipped while disabled (i.e. when the user placed the widget by hand).</summary>
    public bool Enabled { get; set; } = true;

    public IReadOnlyList<Rectangle> Current => Volatile.Read(ref _current);

    public TaskbarObstacles()
    {
        _timer = new System.Threading.Timer(_ => Scan(), null, TimeSpan.Zero, ScanInterval);
    }

    private void Scan()
    {
        if (!Enabled || Interlocked.Exchange(ref _scanning, 1) == 1)
            return;
        try
        {
            var found = Query();
            if (found is null || found.SequenceEqual(Current))
                return;
            Volatile.Write(ref _current, found);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            // Best-effort placement hint running on a timer thread: UI Automation throws a variety
            // of exceptions while Explorer restarts or is busy, and none of them may kill the app.
            // Keep the previous result and try again on the next tick.
        }
        finally
        {
            Volatile.Write(ref _scanning, 0);
        }
    }

    private static IReadOnlyList<Rectangle>? Query()
    {
        var tray = FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero || !GetWindowRect(tray, out var trayRect))
            return null;
        var bounds = trayRect.ToRectangle();
        bool horizontal = bounds.Width >= bounds.Height;
        int trayLength = horizontal ? bounds.Width : bounds.Height;

        var cache = new CacheRequest { AutomationElementMode = AutomationElementMode.None };
        cache.Add(AutomationElement.BoundingRectangleProperty);
        cache.Add(AutomationElement.IsOffscreenProperty);

        // The root must be a live element (fetched outside the cache scope); only the
        // descendants are returned as cached, reference-less snapshots.
        var root = AutomationElement.FromHandle(tray);
        AutomationElementCollection elements;
        using (cache.Activate())
            elements = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);

        var rects = new List<Rectangle>();
        foreach (AutomationElement e in elements)
        {
            if (e.Cached.IsOffscreen)
                continue;
            var r = e.Cached.BoundingRectangle;
            if (r.IsEmpty || r.Width <= 0 || r.Height <= 0)
                continue;
            var rect = new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
            // Skip containers that span most of the taskbar; we only care about individual buttons.
            if ((horizontal ? rect.Width : rect.Height) > trayLength / 2 || !rect.IntersectsWith(bounds))
                continue;
            rects.Add(rect);
        }
        rects.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        return rects;
    }

    public void Dispose() => _timer.Dispose();
}
