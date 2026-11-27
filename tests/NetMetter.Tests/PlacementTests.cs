using System.Drawing;
using NetMetter;

namespace NetMetter.Tests;

public class PlacementTests
{
    private static TaskbarInfo Taskbar(Rectangle bounds, TaskbarEdge edge, Rectangle? notify, int reserved,
        float scale = 1f, string device = @"\\.\DISPLAY1", bool primary = true, Rectangle? monitor = null) =>
        new()
        {
            Handle = IntPtr.Zero,
            Bounds = bounds,
            Edge = edge,
            NotifyArea = notify,
            ReservedBeforeNotifyArea = reserved,
            Scale = scale,
            IsShown = true,
            MonitorDeviceName = device,
            MonitorBounds = monitor ?? bounds,
            IsPrimary = primary,
        };

    // ------------------------------------------------------------------ default position

    [Fact]
    public void Sits_just_left_of_the_notification_area()
    {
        var tb = Taskbar(new Rectangle(0, 1416, 2560, 96), TaskbarEdge.Bottom,
            notify: new Rectangle(2000, 1416, 560, 96), reserved: 0);

        // notify.Left(2000) - margin(4) - length(200)
        Assert.Equal(1796, Placement.Default(tb, length: 200));
    }

    [Fact]
    public void Leaves_room_for_the_widgets_button_when_reserved()
    {
        var tb = Taskbar(new Rectangle(0, 1416, 2560, 96), TaskbarEdge.Bottom,
            notify: new Rectangle(2000, 1416, 560, 96), reserved: 96);

        // (notify.Left(2000) - reserved(96)) - margin(4) - length(200)
        Assert.Equal(1700, Placement.Default(tb, length: 200));
    }

    [Fact]
    public void Falls_back_when_no_notification_area_is_reported()
    {
        var tb = Taskbar(new Rectangle(0, 1416, 2560, 96), TaskbarEdge.Bottom,
            notify: null, reserved: 0);

        // (Bounds.Right(2560) - 200*scale) - margin(4) - length(200)
        Assert.Equal(2156, Placement.Default(tb, length: 200));
    }

    [Fact]
    public void Vertical_taskbar_measures_from_the_top_of_the_notification_area()
    {
        var tb = Taskbar(new Rectangle(0, 0, 72, 1440), TaskbarEdge.Left,
            notify: new Rectangle(0, 1100, 72, 340), reserved: 0);

        // notify.Top(1100) - margin(4) - length(150)
        Assert.Equal(946, Placement.Default(tb, length: 150));
    }

    [Fact]
    public void Margin_scales_with_dpi()
    {
        var tb = Taskbar(new Rectangle(0, 2124, 3840, 144), TaskbarEdge.Bottom,
            notify: new Rectangle(3000, 2124, 840, 144), reserved: 0, scale: 1.5f);

        // notify.Left(3000) - margin(6 = 4*1.5) - length(300)
        Assert.Equal(2694, Placement.Default(tb, length: 300));
    }

    // ------------------------------------------------------------------ keep-out spans

    [Fact]
    public void Keep_out_covers_the_notification_area()
    {
        var tb = Taskbar(new Rectangle(0, 1416, 2560, 96), TaskbarEdge.Bottom,
            notify: new Rectangle(2000, 1416, 560, 96), reserved: 0);

        Assert.Equal([new Placement.KeepOut(2000, 2560)], Placement.KeepOuts(tb));
    }

    [Fact]
    public void Keep_out_includes_the_reserved_widgets_strip()
    {
        var tb = Taskbar(new Rectangle(0, 1416, 2560, 96), TaskbarEdge.Bottom,
            notify: new Rectangle(2000, 1416, 560, 96), reserved: 96);

        Assert.Equal([new Placement.KeepOut(1904, 2560)], Placement.KeepOuts(tb));
    }

    [Fact]
    public void Keep_out_runs_along_the_long_axis_of_a_vertical_taskbar()
    {
        var tb = Taskbar(new Rectangle(0, 0, 72, 1440), TaskbarEdge.Left,
            notify: new Rectangle(0, 1100, 72, 340), reserved: 0);

        Assert.Equal([new Placement.KeepOut(1100, 1440)], Placement.KeepOuts(tb));
    }

    [Fact]
    public void No_keep_out_when_windows_reports_no_notification_area()
    {
        var tb = Taskbar(new Rectangle(0, 1416, 2560, 96), TaskbarEdge.Bottom, notify: null, reserved: 0);

        Assert.Empty(Placement.KeepOuts(tb));
    }

    // ------------------------------------------------------------------ avoiding overlap

    private static readonly IReadOnlyList<Placement.KeepOut> Tray = [new Placement.KeepOut(2000, 2560)];

    [Fact]
    public void A_free_position_is_left_alone()
    {
        Assert.Equal(1200, Placement.Avoid(1200, 200, Tray, min: 0, max: 2560));
    }

    [Fact]
    public void A_position_on_the_notification_area_is_pushed_before_it()
    {
        // Overlaps by 100px; the near side is much closer than the far end.
        Assert.Equal(1800, Placement.Avoid(1900, 200, Tray, min: 0, max: 2560));
    }

    [Fact]
    public void A_position_inside_the_notification_area_goes_to_whichever_side_is_nearer()
    {
        // Starting at 2400 the far edge (2560) is nearer than 1800, but the meter would not fit
        // past it, so it lands before the span instead.
        Assert.Equal(1800, Placement.Avoid(2400, 200, Tray, min: 0, max: 2560));

        // With room on both sides, the smaller move wins: 950 is 150px from clearing the span
        // backwards and 250px from clearing it forwards.
        IReadOnlyList<Placement.KeepOut> middle = [new Placement.KeepOut(1000, 1200)];
        Assert.Equal(800, Placement.Avoid(950, 200, middle, min: 0, max: 2560));
        Assert.Equal(1200, Placement.Avoid(1150, 200, middle, min: 0, max: 2560));
        // A tie goes backwards, towards the end the meter is anchored from.
        Assert.Equal(800, Placement.Avoid(1000, 200, middle, min: 0, max: 2560));
    }

    [Fact]
    public void A_span_at_the_near_end_pushes_the_meter_past_it()
    {
        IReadOnlyList<Placement.KeepOut> nearEnd = [new Placement.KeepOut(0, 300)];

        Assert.Equal(300, Placement.Avoid(0, 200, nearEnd, min: 0, max: 2560));
    }

    [Fact]
    public void A_meter_too_long_for_the_free_space_stays_put_inside_the_taskbar()
    {
        // 600px of meter, a 500px taskbar, and a span across the middle: nowhere to go.
        IReadOnlyList<Placement.KeepOut> span = [new Placement.KeepOut(200, 300)];

        int along = Placement.Avoid(250, 600, span, min: 0, max: 500);
        Assert.Equal(0, along);
    }

    [Fact]
    public void Positions_are_always_clamped_into_the_taskbar()
    {
        Assert.Equal(0, Placement.Avoid(-500, 200, [], min: 0, max: 2560));
        Assert.Equal(2360, Placement.Avoid(9000, 200, [], min: 0, max: 2560));
    }

    [Fact]
    public void Adjacent_spans_are_both_avoided()
    {
        // The Widgets button and the notification area as two touching spans: stepping clear of
        // the first must not land on the second.
        IReadOnlyList<Placement.KeepOut> two =
            [new Placement.KeepOut(1900, 2000), new Placement.KeepOut(2000, 2560)];

        int along = Placement.Avoid(1950, 200, two, min: 0, max: 2560);
        Assert.Equal(1700, along);
        Assert.All(two, span => Assert.False(span.Overlaps(along, 200)));
    }

    // ------------------------------------------------------------------ monitor selection

    [Fact]
    public void Picks_the_taskbar_on_the_chosen_monitor()
    {
        var primary = Taskbar(new Rectangle(0, 1416, 2560, 96), TaskbarEdge.Bottom, null, 0);
        var secondary = Taskbar(new Rectangle(2560, 1016, 1920, 64), TaskbarEdge.Bottom, null, 0,
            device: @"\\.\DISPLAY2", primary: false);

        Assert.Same(secondary, TaskbarInfo.Select([primary, secondary], @"\\.\DISPLAY2"));
        Assert.Same(primary, TaskbarInfo.Select([primary, secondary], @"\\.\DISPLAY1"));
    }

    [Fact]
    public void Falls_back_to_the_primary_taskbar_when_the_chosen_monitor_is_gone()
    {
        var primary = Taskbar(new Rectangle(0, 1416, 2560, 96), TaskbarEdge.Bottom, null, 0);

        Assert.Same(primary, TaskbarInfo.Select([primary], @"\\.\DISPLAY5"));
        Assert.Same(primary, TaskbarInfo.Select([primary], null));
    }

    [Fact]
    public void Falls_back_to_any_taskbar_when_the_primary_one_is_not_listed()
    {
        var secondary = Taskbar(new Rectangle(2560, 1016, 1920, 64), TaskbarEdge.Bottom, null, 0,
            device: @"\\.\DISPLAY2", primary: false);

        Assert.Same(secondary, TaskbarInfo.Select([secondary], null));
        Assert.Null(TaskbarInfo.Select([], null));
    }

    // ------------------------------------------------------------------ docked edge

    [Theory]
    // A secondary monitor to the right of the primary one, 1920x1080 at x=2560. The edge is named
    // rather than passed as a value: TaskbarEdge is internal, and xUnit needs public test methods.
    [InlineData(2560, 1032, 1920, 48, "Bottom")]
    [InlineData(2560, 0, 1920, 48, "Top")]
    [InlineData(2560, 0, 48, 1080, "Left")]
    [InlineData(4432, 0, 48, 1080, "Right")]
    public void Derives_the_docked_edge_from_the_geometry(int x, int y, int w, int h, string expected)
    {
        var monitor = new Rectangle(2560, 0, 1920, 1080);

        Assert.Equal(expected, TaskbarInfo.EdgeFromGeometry(new Rectangle(x, y, w, h), monitor).ToString());
    }

    [Fact]
    public void An_auto_hidden_taskbar_still_reports_the_edge_it_is_docked_to()
    {
        var monitor = new Rectangle(0, 0, 2560, 1440);

        // Slid off the bottom: only two pixels are left on screen, and the rest is past 1440.
        Assert.Equal(TaskbarEdge.Bottom, TaskbarInfo.EdgeFromGeometry(new Rectangle(0, 1438, 2560, 48), monitor));
        // Slid off the top.
        Assert.Equal(TaskbarEdge.Top, TaskbarInfo.EdgeFromGeometry(new Rectangle(0, -46, 2560, 48), monitor));
    }
}
