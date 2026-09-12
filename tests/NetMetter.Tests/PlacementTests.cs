using System.Drawing;
using NetMetter;

namespace NetMetter.Tests;

public class PlacementTests
{
    private static TaskbarInfo Taskbar(Rectangle bounds, TaskbarEdge edge, Rectangle? notify, int reserved, float scale = 1f) =>
        new()
        {
            Handle = IntPtr.Zero,
            Bounds = bounds,
            Edge = edge,
            NotifyArea = notify,
            ReservedBeforeNotifyArea = reserved,
            Scale = scale,
            IsShown = true,
        };

    [Fact]
    public void Sits_just_left_of_the_notification_area()
    {
        var tb = Taskbar(new Rectangle(0, 1416, 2560, 96), TaskbarEdge.Bottom,
            notify: new Rectangle(2000, 1416, 560, 96), reserved: 0);

        // notify.Left(2000) - margin(4) - length(200)
        Assert.Equal(1796, MeterWindow.DefaultTaskbarPosition(tb, length: 200));
    }

    [Fact]
    public void Leaves_room_for_the_widgets_button_when_reserved()
    {
        var tb = Taskbar(new Rectangle(0, 1416, 2560, 96), TaskbarEdge.Bottom,
            notify: new Rectangle(2000, 1416, 560, 96), reserved: 96);

        // (notify.Left(2000) - reserved(96)) - margin(4) - length(200)
        Assert.Equal(1700, MeterWindow.DefaultTaskbarPosition(tb, length: 200));
    }

    [Fact]
    public void Falls_back_when_no_notification_area_is_reported()
    {
        var tb = Taskbar(new Rectangle(0, 1416, 2560, 96), TaskbarEdge.Bottom,
            notify: null, reserved: 0);

        // (Bounds.Right(2560) - 200*scale) - margin(4) - length(200)
        Assert.Equal(2156, MeterWindow.DefaultTaskbarPosition(tb, length: 200));
    }

    [Fact]
    public void Vertical_taskbar_measures_from_the_top_of_the_notification_area()
    {
        var tb = Taskbar(new Rectangle(0, 0, 72, 1440), TaskbarEdge.Left,
            notify: new Rectangle(0, 1100, 72, 340), reserved: 0);

        // notify.Top(1100) - margin(4) - length(150)
        Assert.Equal(946, MeterWindow.DefaultTaskbarPosition(tb, length: 150));
    }

    [Fact]
    public void Margin_scales_with_dpi()
    {
        var tb = Taskbar(new Rectangle(0, 2124, 3840, 144), TaskbarEdge.Bottom,
            notify: new Rectangle(3000, 2124, 840, 144), reserved: 0, scale: 1.5f);

        // notify.Left(3000) - margin(6 = 4*1.5) - length(300)
        Assert.Equal(2694, MeterWindow.DefaultTaskbarPosition(tb, length: 300));
    }
}
