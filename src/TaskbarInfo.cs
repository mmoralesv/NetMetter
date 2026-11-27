using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using static NetMetter.NativeMethods;

namespace NetMetter;

internal enum TaskbarEdge { Left, Top, Right, Bottom }

/// <summary>
/// Geometry of one taskbar, in physical pixels. There is one per monitor while "Show my taskbar
/// on all displays" is on: the primary (<c>Shell_TrayWnd</c>) and a secondary taskbar
/// (<c>Shell_SecondaryTrayWnd</c>) for each of the other monitors.
/// </summary>
internal sealed class TaskbarInfo
{
    public required IntPtr Handle { get; init; }
    public required Rectangle Bounds { get; init; }
    public required TaskbarEdge Edge { get; init; }
    /// <summary>Rectangle of the notification area (tray icons + clock), if Windows exposes one.</summary>
    public Rectangle? NotifyArea { get; init; }
    /// <summary>
    /// Space to leave free just before the notification area. On Windows 11 with a left-aligned
    /// taskbar the Widgets button sits there; Windows exposes no supported way to ask for its
    /// position, and one taskbar button is about as wide as the taskbar is tall.
    /// </summary>
    public int ReservedBeforeNotifyArea { get; init; }
    public required float Scale { get; init; }
    /// <summary>False while an auto-hide taskbar is slid off screen.</summary>
    public required bool IsShown { get; init; }
    /// <summary>
    /// The monitor this taskbar is docked to, as a <c>\\.\DISPLAYn</c> device name. The user's
    /// monitor choice is stored as this name: it survives restarts and matches
    /// <see cref="Screen.DeviceName"/>.
    /// </summary>
    public required string MonitorDeviceName { get; init; }
    public required Rectangle MonitorBounds { get; init; }
    /// <summary>True for the primary taskbar — the one that carries the notification area.</summary>
    public required bool IsPrimary { get; init; }

    public bool IsHorizontal => Edge is TaskbarEdge.Top or TaskbarEdge.Bottom;
    /// <summary>Height of a horizontal taskbar, width of a vertical one.</summary>
    public int Thickness => IsHorizontal ? Bounds.Height : Bounds.Width;

    // The shell queries below are the slow part of a refresh: SHAppBarMessage sends a message to
    // Explorer and blocks the calling thread while Explorer is busy, and the preference lookups hit
    // the registry. Query() runs on the UI thread once a second, so their results are cached and
    // refreshed only occasionally; the docked edge and the taskbar preferences change very rarely.
    private static readonly TimeSpan ShellCacheLifetime = TimeSpan.FromSeconds(5);
    private static readonly System.Diagnostics.Stopwatch ShellClock = System.Diagnostics.Stopwatch.StartNew();
    private static TimeSpan _shellCachedAt;
    private static TaskbarEdge _cachedEdge;
    private static bool _cachedReserveWidgets;
    private static bool _shellCacheValid;

    /// <summary>Drops the cached shell/preference lookups, so the next query re-reads them.</summary>
    public static void InvalidateShellCache() => _shellCacheValid = false;

    /// <summary>
    /// The taskbar the meter should attach to: the monitor the user picked, or the primary one.
    /// <paramref name="preferredMonitor"/> is a <c>\\.\DISPLAYn</c> device name, or null for the
    /// primary taskbar.
    /// </summary>
    public static TaskbarInfo? Query(string? preferredMonitor = null) => Select(QueryAll(), preferredMonitor);

    /// <summary>Every taskbar currently on screen, the primary one first.</summary>
    public static IReadOnlyList<TaskbarInfo> QueryAll()
    {
        var all = new List<TaskbarInfo>(2);

        if (Describe(FindWindow("Shell_TrayWnd", null), isPrimary: true) is { } primary)
            all.Add(primary);

        // Secondary taskbars are separate top-level windows, one per monitor, and exist only while
        // "Show my taskbar on all displays" is on. FindWindowEx with a null parent walks the
        // desktop's children — the top-level windows — so this enumerates them without a callback.
        var next = IntPtr.Zero;
        while ((next = FindWindowEx(IntPtr.Zero, next, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
        {
            if (Describe(next, isPrimary: false) is { } secondary)
                all.Add(secondary);
        }
        return all;
    }

    /// <summary>
    /// Picks the preferred monitor's taskbar, falling back to the primary one. A monitor that has
    /// been unplugged, or whose taskbar the user has since turned off, falls back instead of
    /// leaving the meter with nothing to attach to, which would look like a crash.
    /// </summary>
    internal static TaskbarInfo? Select(IReadOnlyList<TaskbarInfo> taskbars, string? preferredMonitor)
    {
        if (taskbars.Count == 0)
            return null;

        if (!string.IsNullOrEmpty(preferredMonitor))
        {
            foreach (var tb in taskbars)
            {
                if (string.Equals(tb.MonitorDeviceName, preferredMonitor, StringComparison.OrdinalIgnoreCase))
                    return tb;
            }
        }

        foreach (var tb in taskbars)
        {
            if (tb.IsPrimary)
                return tb;
        }
        return taskbars[0];
    }

    private static TaskbarInfo? Describe(IntPtr tray, bool isPrimary)
    {
        if (tray == IntPtr.Zero || !GetWindowRect(tray, out var r))
            return null;

        var bounds = r.ToRectangle();
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return null;

        var (monitorBounds, device) = MonitorOf(tray);
        var edge = isPrimary ? PrimaryEdge(bounds, monitorBounds) : EdgeFromGeometry(bounds, monitorBounds);

        var visible = Rectangle.Intersect(bounds, monitorBounds);
        bool horizontal = edge is TaskbarEdge.Top or TaskbarEdge.Bottom;
        int visibleThickness = horizontal ? visible.Height : visible.Width;
        int thickness = horizontal ? bounds.Height : bounds.Width;

        Rectangle? notify = null;
        var notifyWnd = FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
        if (notifyWnd != IntPtr.Zero && GetWindowRect(notifyWnd, out var nr))
        {
            var n = nr.ToRectangle();
            int length = horizontal ? n.Width : n.Height;
            int trayLength = horizontal ? bounds.Width : bounds.Height;
            // Sanity check: some shell builds report an empty or full-width rectangle.
            if (length > 0 && length < trayLength / 2 && bounds.Contains(n))
                notify = n;
        }

        uint dpi = GetDpiForWindow(tray);
        return new TaskbarInfo
        {
            Handle = tray,
            Bounds = bounds,
            Edge = edge,
            NotifyArea = notify,
            // Only the primary taskbar carries the Widgets button; a secondary one shows app
            // buttons and a clock.
            ReservedBeforeNotifyArea = isPrimary && _cachedReserveWidgets ? thickness : 0,
            Scale = dpi == 0 ? 1f : dpi / 96f,
            IsShown = IsWindowVisible(tray) && visibleThickness >= thickness / 2,
            MonitorDeviceName = device,
            MonitorBounds = monitorBounds,
            IsPrimary = isPrimary,
        };
    }

    /// <summary>
    /// Which screen edge the primary taskbar is docked to. GetWindowRect gives the *current*
    /// position (it moves off screen when auto-hidden), whereas the app-bar message gives the
    /// docked edge. That message is the expensive call, so its result — and the Widgets-button
    /// preference read alongside it — is cached.
    /// </summary>
    private static TaskbarEdge PrimaryEdge(Rectangle bounds, Rectangle monitor)
    {
        var now = ShellClock.Elapsed;
        if (!_shellCacheValid || now - _shellCachedAt > ShellCacheLifetime)
        {
            _cachedEdge = QueryDockedEdge(bounds, monitor);
            _cachedReserveWidgets = ShouldReserveWidgetsButton();
            _shellCachedAt = now;
            _shellCacheValid = true;
        }
        return _cachedEdge;
    }

    private static TaskbarEdge QueryDockedEdge(Rectangle bounds, Rectangle monitor)
    {
        var abd = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
        return SHAppBarMessage(ABM_GETTASKBARPOS, ref abd) != UIntPtr.Zero
            ? abd.uEdge switch
            {
                ABE_LEFT => TaskbarEdge.Left,
                ABE_TOP => TaskbarEdge.Top,
                ABE_RIGHT => TaskbarEdge.Right,
                _ => TaskbarEdge.Bottom,
            }
            : EdgeFromGeometry(bounds, monitor);
    }

    /// <summary>
    /// Which edge of its monitor a taskbar is docked to, worked out from its shape and position.
    /// This is the only option for a secondary taskbar: the app-bar message only ever reports the
    /// primary one. Comparing midpoints rather than gaps keeps the answer right while an auto-hide
    /// taskbar is slid off the edge.
    /// </summary>
    internal static TaskbarEdge EdgeFromGeometry(Rectangle bounds, Rectangle monitor)
    {
        if (bounds.Width >= bounds.Height)
        {
            return bounds.Top + bounds.Height / 2 < monitor.Top + monitor.Height / 2
                ? TaskbarEdge.Top
                : TaskbarEdge.Bottom;
        }
        return bounds.Left + bounds.Width / 2 < monitor.Left + monitor.Width / 2
            ? TaskbarEdge.Left
            : TaskbarEdge.Right;
    }

    /// <summary>
    /// True when the Windows 11 Widgets button sits next to the notification area, which is what it
    /// does while the taskbar is left-aligned. Reading these preferences is enough; nothing is
    /// written, and on any other configuration this is false.
    /// </summary>
    private static bool ShouldReserveWidgetsButton()
    {
        if (Environment.OSVersion.Version.Build < 22000)
            return false;

        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
        bool leftAligned = key?.GetValue("TaskbarAl") is int alignment && alignment == 0;
        bool widgetsShown = key?.GetValue("TaskbarDa") is not int widgets || widgets != 0; // absent means shown
        return leftAligned && widgetsShown;
    }

    /// <summary>
    /// True when a full-screen app (video, game, presentation) is in the foreground on the given
    /// monitor. Windows hides the taskbar in that case, so the meter hides too.
    /// </summary>
    public static bool IsFullscreenAppActive(IntPtr ownWindow, Rectangle monitor)
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == ownWindow || fg == GetShellWindow())
            return false;

        var cls = new StringBuilder(64);
        GetClassName(fg, cls, cls.Capacity);
        if (cls.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
            return false;

        // A maximized window can also cover the whole monitor (e.g. with an auto-hide taskbar).
        if (IsZoomed(fg) || !GetWindowRect(fg, out var r))
            return false;

        var w = r.ToRectangle();
        return w.Left <= monitor.Left && w.Top <= monitor.Top
            && w.Right >= monitor.Right && w.Bottom >= monitor.Bottom;
    }

    /// <summary>True when a full-screen app covers this taskbar's monitor.</summary>
    public bool IsFullscreenAppActive(IntPtr ownWindow) => IsFullscreenAppActive(ownWindow, MonitorBounds);

    /// <summary>Bounds and device name of the monitor a window sits on.</summary>
    private static (Rectangle Bounds, string Device) MonitorOf(IntPtr hwnd)
    {
        var mi = new MONITORINFOEX { cbSize = (uint)Marshal.SizeOf<MONITORINFOEX>() };
        var hmon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (hmon != IntPtr.Zero && GetMonitorInfoEx(hmon, ref mi))
            return (mi.rcMonitor.ToRectangle(), mi.szDevice ?? string.Empty);

        var primary = Screen.PrimaryScreen!;
        return (primary.Bounds, primary.DeviceName);
    }

    public static Rectangle GetMonitorBounds(IntPtr hwnd) => MonitorOf(hwnd).Bounds;
}
