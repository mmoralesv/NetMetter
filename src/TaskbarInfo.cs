using System.Text;
using static NetMetter.NativeMethods;

namespace NetMetter;

internal enum TaskbarEdge { Left, Top, Right, Bottom }

/// <summary>Geometry of the primary taskbar, in physical pixels.</summary>
internal sealed class TaskbarInfo
{
    public required IntPtr Handle { get; init; }
    public required Rectangle Bounds { get; init; }
    public required TaskbarEdge Edge { get; init; }
    /// <summary>Rectangle of the notification area (tray icons + clock), if Windows exposes one.</summary>
    public Rectangle? NotifyArea { get; init; }
    public required float Scale { get; init; }
    /// <summary>False while an auto-hide taskbar is slid off screen.</summary>
    public required bool IsShown { get; init; }

    public bool IsHorizontal => Edge is TaskbarEdge.Top or TaskbarEdge.Bottom;
    /// <summary>Height of a horizontal taskbar, width of a vertical one.</summary>
    public int Thickness => IsHorizontal ? Bounds.Height : Bounds.Width;

    public static TaskbarInfo? Query()
    {
        var tray = FindWindow("Shell_TrayWnd", null);
        if (tray == IntPtr.Zero || !GetWindowRect(tray, out var r))
            return null;

        // GetWindowRect gives the *current* position (it moves off screen when auto-hidden),
        // whereas the app-bar message tells us which screen edge the taskbar is docked to.
        var bounds = r.ToRectangle();
        var abd = new APPBARDATA { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<APPBARDATA>() };
        var edge = SHAppBarMessage(ABM_GETTASKBARPOS, ref abd) != UIntPtr.Zero
            ? abd.uEdge switch
            {
                ABE_LEFT => TaskbarEdge.Left,
                ABE_TOP => TaskbarEdge.Top,
                ABE_RIGHT => TaskbarEdge.Right,
                _ => TaskbarEdge.Bottom,
            }
            : bounds.Width >= bounds.Height ? TaskbarEdge.Bottom : TaskbarEdge.Left;

        var monitor = GetMonitorBounds(tray);
        var visible = Rectangle.Intersect(bounds, monitor);
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
            Scale = dpi == 0 ? 1f : dpi / 96f,
            IsShown = IsWindowVisible(tray) && visibleThickness >= thickness / 2,
        };
    }

    /// <summary>
    /// True when the foreground window is a full-screen app (video, game, presentation) on the
    /// taskbar's monitor. Windows hides the taskbar in that case, so we must hide too.
    /// </summary>
    public bool IsFullscreenAppActive(IntPtr ownWindow)
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == ownWindow || fg == Handle || fg == GetShellWindow())
            return false;

        var cls = new StringBuilder(64);
        GetClassName(fg, cls, cls.Capacity);
        if (cls.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
            return false;

        // A maximized window can also cover the whole monitor (e.g. with an auto-hide taskbar).
        if (IsZoomed(fg) || !GetWindowRect(fg, out var r))
            return false;

        var monitor = GetMonitorBounds(Handle);
        var w = r.ToRectangle();
        return w.Left <= monitor.Left && w.Top <= monitor.Top
            && w.Right >= monitor.Right && w.Bottom >= monitor.Bottom;
    }

    private static Rectangle GetMonitorBounds(IntPtr hwnd)
    {
        var mi = new MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        var hmon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        return GetMonitorInfo(hmon, ref mi) ? mi.rcMonitor.ToRectangle() : Screen.PrimaryScreen!.Bounds;
    }
}
