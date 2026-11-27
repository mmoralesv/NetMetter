using System.Diagnostics;
using System.Text;
using static NetMetter.NativeMethods;

namespace NetMetter;

/// <summary>
/// Answers one question: is another app drawing on top of the meter? The meter is a top-most window
/// and re-asserts that whenever the foreground changes, but another top-most window — a second
/// desktop widget, a replacement shell, a presentation or screen-recording overlay — can still end
/// up above it. Finding that out is what turns "the meter stopped working" into "something is
/// covering the meter".
/// </summary>
internal static class Occlusion
{
    /// <summary>
    /// The window drawn over <paramref name="rect"/>, or <see cref="IntPtr.Zero"/> when the meter
    /// has those pixels to itself.
    /// </summary>
    /// <remarks>
    /// This walks the z-order rather than asking <c>WindowFromPoint</c> who owns a pixel, which
    /// looks simpler but answers a different question: Windows keeps a full-screen, invisible
    /// input-occlusion frame in front of everything, so it owns every pixel on the desktop and
    /// hides whatever is actually on screen.
    /// </remarks>
    public static IntPtr Covering(IntPtr self, Rectangle rect)
    {
        if (self == IntPtr.Zero || rect.Width <= 0 || rect.Height <= 0)
            return IntPtr.Zero;

        uint own = (uint)Environment.ProcessId;
        uint shell = ProcessIdOf(GetShellWindow());

        // GW_HWNDPREV steps towards the front of the z-order, so this visits exactly the windows
        // in front of the meter and then stops, instead of walking the whole desktop. The meter is
        // top-most, so there are rarely many.
        for (var hwnd = GetWindow(self, GW_HWNDPREV); hwnd != IntPtr.Zero; hwnd = GetWindow(hwnd, GW_HWNDPREV))
        {
            // Cheapest tests first: almost every window is either hidden or somewhere else.
            if (!IsWindowVisible(hwnd) || !GetWindowRect(hwnd, out var r))
                continue;
            if (!r.ToRectangle().IntersectsWith(rect))
                continue;

            uint pid = ProcessIdOf(hwnd);
            if (pid == own)
                continue; // The meter's own tooltip or context menu.

            // The shell is excluded deliberately. The taskbar coming forward after a click is
            // normal and is what re-asserting the z-order handles, and Explorer also keeps
            // invisible input-occlusion frames in front of the whole desktop. Neither is something
            // the user could act on.
            if (pid != 0 && pid == shell)
                continue;

            if (DrawsPixels(hwnd))
                return hwnd;
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Rules out windows that sit in front of the meter without putting anything on screen: ones
    /// DWM has cloaked (a minimized store app, another virtual desktop) and ones faded to a
    /// near-zero alpha.
    /// </summary>
    private static bool DrawsPixels(IntPtr hwnd)
    {
        if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0)
            return false;

        // Per-pixel layered windows (the meter itself is one) report no attributes here, so this
        // only catches the whole-window alpha case.
        if (GetLayeredWindowAttributes(hwnd, out _, out byte alpha, out uint flags) && (flags & LWA_ALPHA) != 0)
            return alpha > 8;

        return true;
    }

    private static uint ProcessIdOf(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return 0;
        GetWindowThreadProcessId(hwnd, out uint pid);
        return pid;
    }

    /// <summary>
    /// A name for the log, and for telling the user which app is in the way. Falls back from the
    /// process name to the window title to the window class, so there is always something to show.
    /// </summary>
    public static string Describe(IntPtr hwnd)
    {
        var cls = new StringBuilder(96);
        GetClassName(hwnd, cls, cls.Capacity);
        var title = new StringBuilder(160);
        GetWindowText(hwnd, title, title.Capacity);

        string detail = title.Length > 0 ? title.ToString() : cls.ToString();
        string process = ProcessName(hwnd);
        if (process.Length == 0)
            return detail.Length > 0 ? detail : "another window";
        return detail.Length > 0 && !string.Equals(detail, process, StringComparison.OrdinalIgnoreCase)
            ? $"{process} ({detail})"
            : process;
    }

    private static string ProcessName(IntPtr hwnd)
    {
        try
        {
            uint pid = ProcessIdOf(hwnd);
            if (pid == 0)
                return string.Empty;
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // The window's process ended between the probe and here; the class name will do.
            return string.Empty;
        }
    }
}
