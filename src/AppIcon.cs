using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace NetMetter;

/// <summary>
/// The app icon (blue tile with an up and a down arrow), drawn in code so the tray icon is crisp
/// at any DPI. tools/AssetGen uses the same drawing to produce the .ico and the MSIX logos.
/// </summary>
internal static class AppIcon
{
    public static Icon Create(int size = 32)
    {
        using var bmp = Render(size);
        IntPtr handle = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    /// <summary>Draws the icon on a transparent square bitmap of the given size.</summary>
    public static Bitmap Render(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.ScaleTransform(size / 32f, size / 32f);

        using (var tile = RoundedRect(new RectangleF(1, 1, 30, 30), 7))
        using (var background = new SolidBrush(Color.FromArgb(0, 120, 212)))
            g.FillPath(background, tile);

        PointF[] up = [new(11, 5), new(17, 12), new(13, 12), new(13, 27), new(9, 27), new(9, 12), new(5, 12)];
        PointF[] down = [new(19, 5), new(23, 5), new(23, 20), new(27, 20), new(21, 27), new(15, 20), new(19, 20)];
        g.FillPolygon(Brushes.White, up);
        using var downBrush = new SolidBrush(Color.FromArgb(190, 235, 255));
        g.FillPolygon(downBrush, down);
        return bmp;
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
