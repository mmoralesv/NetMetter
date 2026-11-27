namespace NetMetter;

/// <summary>
/// Where the meter sits along a taskbar. This is pure geometry on purpose: the awkward cases — a
/// notification area halfway down a vertical taskbar, a saved offset from a monitor that is no
/// longer attached, a drag that lands on the clock — are covered by unit tests instead of being
/// discovered on someone's desktop.
/// </summary>
internal static class Placement
{
    /// <summary>A span along the taskbar's long axis that the meter must not cover.</summary>
    internal readonly record struct KeepOut(int Start, int End)
    {
        public bool Overlaps(int start, int length) => End > start && Start < start + length;
    }

    /// <summary>
    /// The parts of the taskbar that belong to Windows: the notification area (tray icons and
    /// clock) and, on Windows 11 with a left-aligned taskbar, the Widgets button beside it. Both
    /// come from documented sources — the notification area's own window and the taskbar layout
    /// preferences — so nothing here reaches into taskbar internals.
    /// </summary>
    public static IReadOnlyList<KeepOut> KeepOuts(TaskbarInfo tb)
    {
        if (tb.NotifyArea is not { } notify)
            return [];
        int start = tb.IsHorizontal ? notify.Left : notify.Top;
        int end = tb.IsHorizontal ? notify.Right : notify.Bottom;
        return [new KeepOut(start - tb.ReservedBeforeNotifyArea, end)];
    }

    /// <summary>Just before the notification area, leaving room for the Widgets button if it's there.</summary>
    public static int Default(TaskbarInfo tb, int length)
    {
        int margin = (int)(4 * tb.Scale);
        int farEnd = tb.IsHorizontal ? tb.Bounds.Right : tb.Bounds.Bottom;

        // A secondary taskbar has no notification-area window to ask about, so leave a clock's
        // worth of room at the far end instead.
        int limit = tb.NotifyArea is { } notify
            ? (tb.IsHorizontal ? notify.Left : notify.Top) - tb.ReservedBeforeNotifyArea
            : farEnd - (int)(200 * tb.Scale);
        return limit - margin - length;
    }

    /// <summary>
    /// Moves <paramref name="along"/> clear of every keep-out span, to whichever side is nearer and
    /// still fits between <paramref name="min"/> and <paramref name="max"/>. When the meter is
    /// simply longer than the free space it stays where it was, clamped into the taskbar.
    /// </summary>
    public static int Avoid(int along, int length, IReadOnlyList<KeepOut> keepOuts, int min, int max)
    {
        int limit = Math.Max(min, max - length);
        along = Math.Clamp(along, min, limit);

        // Several passes: stepping clear of one span can land on the next, and the spans are not
        // guaranteed to be in order.
        for (int pass = 0; pass <= keepOuts.Count; pass++)
        {
            bool moved = false;
            foreach (var keepOut in keepOuts)
            {
                if (!keepOut.Overlaps(along, length))
                    continue;

                int before = keepOut.Start - length, after = keepOut.End;
                bool beforeFits = before >= min, afterFits = after <= limit;
                if (beforeFits && (!afterFits || along - before <= after - along))
                    along = before;
                else if (afterFits)
                    along = after;
                else
                    return along; // No gap wide enough on either side.
                moved = true;
            }
            if (!moved)
                break;
        }
        return along;
    }
}
