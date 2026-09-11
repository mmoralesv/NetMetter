using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using static NetMetter.NativeMethods;

namespace NetMetter;

/// <summary>
/// A borderless, click-through-looking window that sits on top of the taskbar and draws one
/// column per network interface. Windows 11 dropped support for taskbar "desk bands", so the
/// widget is a top-most layered window that tracks the taskbar's position instead.
/// </summary>
internal sealed class TaskbarWidget : Form
{
    private const string FontFamilyName = "Segoe UI";
    private const string NameFontFamilyName = "Segoe UI Semibold";

    private readonly AppSettings _settings;
    private readonly ToolTip _toolTip = new() { ShowAlways = true, InitialDelay = 400, AutoPopDelay = 30000 };
    private readonly List<(RectangleF Rect, InterfaceStat Stat)> _hitZones = [];
    private readonly WinEventDelegate _foregroundChangedProc;
    private readonly IntPtr _foregroundHook;
    private readonly StringFormat _leftFormat;
    private readonly StringFormat _rightFormat;
    private readonly TaskbarObstacles _obstacles = new();

    private IReadOnlyList<InterfaceStat> _items = [];
    private TaskbarInfo? _taskbar;
    private Point _position;
    private Size _size;
    private string? _toolTipId;

    private Font? _valueFont, _nameFont;
    private float _fontPx;

    private bool _dragging;
    private int _dragStartCursor, _dragStartPos, _dragPos;

    /// <summary>Raised when the user finishes dragging the widget to a new spot.</summary>
    public event EventHandler? PositionChanged;

    public TaskbarWidget(AppSettings settings)
    {
        _settings = settings;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        TopMost = true;
        Size = new Size(1, 1);
        Text = "NetMetter";

        _leftFormat = new StringFormat(StringFormat.GenericTypographic)
        {
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        _rightFormat = new StringFormat(_leftFormat) { Alignment = StringAlignment.Far };

        // Clicking the taskbar raises it above us; re-assert our z-order as soon as that happens
        // (and hide promptly when a full-screen app takes the foreground).
        _foregroundChangedProc = (_, _, _, _, _, _, _) => Render();
        _foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _foregroundChangedProc, 0, 0, WINEVENT_OUTOFCONTEXT);

        _obstacles.Changed += (_, _) =>
        {
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke(Render);
        };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    public void UpdateItems(IReadOnlyList<InterfaceStat> items)
    {
        _items = items;
        Render();
    }

    /// <summary>Forgets any dragged position and goes back to sitting next to the tray.</summary>
    public void ResetPosition()
    {
        _settings.Anchor = AnchorSide.Auto;
        _settings.AnchorOffset = 0;
        Render();
    }

    public void Render()
    {
        if (IsDisposed)
            return;

        _obstacles.Enabled = _settings.Anchor == AnchorSide.Auto;
        var tb = TaskbarInfo.Query();
        _taskbar = tb;
        if (tb is null || !tb.IsShown || tb.IsFullscreenAppActive(Handle))
        {
            if (Visible)
                Hide();
            return;
        }

        // Show before painting: WinForms applies its own bounds on first show, and a layered
        // window stays invisible until UpdateLayeredWindow gives it content and its real bounds.
        if (!Visible)
            Show();

        var layout = ComputeLayout(tb);
        _size = layout.Size;
        _position = ComputePosition(tb, _size);
        PaintLayered(layout);

        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

        if (_toolTipId is not null)
            RefreshToolTip();
    }

    // ---------------------------------------------------------------- layout

    private readonly record struct WidgetLayout(Size Size, int Lines, float LineHeight, float ColumnWidth,
        IReadOnlyList<PointF> Origins);

    private WidgetLayout ComputeLayout(TaskbarInfo tb)
    {
        string sample = SpeedFormatter.WidestSample(_settings.UseBits);
        int lines = _settings.ShowNames ? 3 : 2;
        float thickness = tb.Thickness;
        float maxFont = 12f * tb.Scale;

        float fontPx;
        if (tb.IsHorizontal)
        {
            fontPx = Math.Min(thickness * 0.86f / lines / 1.25f, maxFont);
            if (lines == 3 && fontPx < 8f * tb.Scale)
            {
                // Small-icon taskbars are too short for three lines; names stay in the tooltip.
                lines = 2;
                fontPx = Math.Min(thickness * 0.86f / lines / 1.25f, maxFont);
            }
        }
        else
        {
            // Vertical taskbar: size the text so the widest column fits the taskbar's width.
            EnsureFonts(10f);
            float widthAt10 = MeasureColumnWidth(sample);
            fontPx = Math.Min(maxFont, 10f * thickness * 0.88f / widthAt10);
        }

        EnsureFonts(fontPx);
        float lineHeight = fontPx * 1.25f;
        float columnWidth = MathF.Ceiling(MeasureColumnWidth(sample));
        if (_items.Count == 0)
            columnWidth = Math.Max(columnWidth, Measure("No network", _valueFont!));

        int count = Math.Max(1, _items.Count);
        float blockHeight = lines * lineHeight;
        var origins = new List<PointF>(count);
        Size size;

        if (tb.IsHorizontal)
        {
            float pad = fontPx * 0.6f, gap = fontPx * 1.1f;
            float top = (thickness - blockHeight) / 2f;
            for (int i = 0; i < count; i++)
                origins.Add(new PointF(pad + i * (columnWidth + gap), top));
            size = new Size((int)MathF.Ceiling(2 * pad + count * columnWidth + (count - 1) * gap), (int)thickness);
        }
        else
        {
            float pad = fontPx * 0.5f, gap = fontPx * 0.8f;
            float left = (thickness - columnWidth) / 2f;
            for (int i = 0; i < count; i++)
                origins.Add(new PointF(left, pad + i * (blockHeight + gap)));
            size = new Size((int)thickness, (int)MathF.Ceiling(2 * pad + count * blockHeight + (count - 1) * gap));
        }

        return new WidgetLayout(size, lines, lineHeight, columnWidth, origins);
    }

    private float MeasureColumnWidth(string sample) =>
        Measure("↓", _valueFont!) + _fontPx * 0.35f + Measure(sample, _valueFont!);

    private void EnsureFonts(float px)
    {
        if (_valueFont is not null && Math.Abs(px - _fontPx) < 0.01f)
            return;
        _valueFont?.Dispose();
        _nameFont?.Dispose();
        _fontPx = px;
        _valueFont = new Font(FontFamilyName, px, FontStyle.Regular, GraphicsUnit.Pixel);
        _nameFont = new Font(NameFontFamilyName, px, FontStyle.Regular, GraphicsUnit.Pixel);
    }

    private static readonly Graphics Measurer = CreateMeasurer();

    private static Graphics CreateMeasurer()
    {
        var g = Graphics.FromImage(new Bitmap(1, 1));
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        return g;
    }

    private static float Measure(string text, Font font) =>
        Measurer.MeasureString(text, font, PointF.Empty, StringFormat.GenericTypographic).Width;

    /// <summary>Where the widget goes along the taskbar, honouring the user's dragged position.</summary>
    private Point ComputePosition(TaskbarInfo tb, Size size)
    {
        int length = tb.IsHorizontal ? size.Width : size.Height;
        int start = tb.IsHorizontal ? tb.Bounds.Left : tb.Bounds.Top;
        int end = tb.IsHorizontal ? tb.Bounds.Right : tb.Bounds.Bottom;

        int along;
        if (_dragging)
            along = _dragPos;
        else
            along = _settings.Anchor switch
            {
                AnchorSide.Near => start + _settings.AnchorOffset,
                AnchorSide.Far => end - _settings.AnchorOffset - length,
                _ => DefaultPosition(tb, length),
            };

        along = Math.Clamp(along, start, Math.Max(start, end - length));
        return tb.IsHorizontal ? new Point(along, tb.Bounds.Top) : new Point(tb.Bounds.Left, along);
    }

    /// <summary>
    /// Just before the notification area, slid towards the start of the taskbar until it no longer
    /// covers any taskbar button. If there's no free gap, it overlaps rather than jumping far away.
    /// </summary>
    private int DefaultPosition(TaskbarInfo tb, int length)
    {
        int margin = (int)(4 * tb.Scale);
        int start = tb.IsHorizontal ? tb.Bounds.Left : tb.Bounds.Top;
        int limit = tb.NotifyArea is { } n
            ? (tb.IsHorizontal ? n.Left : n.Top)
            : (tb.IsHorizontal ? tb.Bounds.Right : tb.Bounds.Bottom) - (int)(200 * tb.Scale);
        int preferredEnd = limit - margin;

        int endPos = preferredEnd;
        bool moved = true;
        while (moved)
        {
            moved = false;
            foreach (var r in _obstacles.Current)
            {
                int a = tb.IsHorizontal ? r.Left : r.Top;
                int b = tb.IsHorizontal ? r.Right : r.Bottom;
                if (a < endPos && b > endPos - length)
                {
                    endPos = a - margin;
                    moved = true;
                }
            }
        }

        return endPos - length >= start ? endPos - length : preferredEnd - length;
    }

    // ---------------------------------------------------------------- painting

    private void PaintLayered(WidgetLayout layout)
    {
        int w = layout.Size.Width, h = layout.Size.Height;
        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        var header = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = w,
            biHeight = -h, // top-down
            biPlanes = 1,
            biBitCount = 32,
        };
        IntPtr dib = CreateDIBSection(memDc, ref header, 0, out IntPtr bits, IntPtr.Zero, 0);
        IntPtr oldBitmap = SelectObject(memDc, dib);
        try
        {
            // Draw straight into the DIB's memory; PArgb matches what UpdateLayeredWindow expects.
            using (var bmp = new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, bits))
            using (var g = Graphics.FromImage(bmp))
                Draw(g, layout);

            var dst = new POINT(_position.X, _position.Y);
            var size = new SIZE(w, h);
            var src = new POINT(0, 0);
            var blend = new BLENDFUNCTION
            {
                BlendOp = AC_SRC_OVER,
                SourceConstantAlpha = 255,
                AlphaFormat = AC_SRC_ALPHA,
            };
            UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);
        }
        finally
        {
            SelectObject(memDc, oldBitmap);
            DeleteObject(dib);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private void Draw(Graphics g, WidgetLayout layout)
    {
        // Alpha 1 is invisible but keeps the whole rectangle clickable (alpha 0 is click-through).
        g.Clear(Color.FromArgb(1, 0, 0, 0));
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var palette = Palette.Current();
        using var textBrush = new SolidBrush(palette.Text);
        using var nameBrush = new SolidBrush(palette.Name);
        using var upBrush = new SolidBrush(palette.Up);
        using var downBrush = new SolidBrush(palette.Down);

        _hitZones.Clear();

        if (_items.Count == 0)
        {
            var o = layout.Origins[0];
            var rect = new RectangleF(o.X, o.Y, layout.ColumnWidth, layout.Lines * layout.LineHeight);
            using var centered = new StringFormat(_leftFormat) { Alignment = StringAlignment.Center };
            g.DrawString("No network", _valueFont!, nameBrush, rect, centered);
            return;
        }

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var o = layout.Origins[i];
            float y = o.Y;

            if (layout.Lines == 3)
            {
                g.DrawString(item.Name, _nameFont!, nameBrush,
                    new RectangleF(o.X, y, layout.ColumnWidth, layout.LineHeight), _leftFormat);
                y += layout.LineHeight;
            }

            DrawSpeedLine(g, "↑", item.UpBytesPerSec, upBrush, textBrush, o.X, y, layout);
            y += layout.LineHeight;
            DrawSpeedLine(g, "↓", item.DownBytesPerSec, downBrush, textBrush, o.X, y, layout);

            _hitZones.Add((new RectangleF(o.X, 0, layout.ColumnWidth, layout.Size.Height), item));
        }
    }

    private void DrawSpeedLine(Graphics g, string arrow, double bytesPerSec, Brush arrowBrush, Brush textBrush,
        float x, float y, WidgetLayout layout)
    {
        var line = new RectangleF(x, y, layout.ColumnWidth, layout.LineHeight);
        g.DrawString(arrow, _valueFont!, arrowBrush, line, _leftFormat);
        g.DrawString(SpeedFormatter.Format(bytesPerSec, _settings.UseBits), _valueFont!, textBrush, line, _rightFormat);
    }

    private readonly record struct Palette(Color Text, Color Name, Color Up, Color Down)
    {
        public static Palette Current()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            bool light = key?.GetValue("SystemUsesLightTheme") is int v && v != 0;
            return light
                ? new Palette(Color.FromArgb(20, 20, 20), Color.FromArgb(200, 20, 20, 20),
                    Color.FromArgb(196, 80, 10), Color.FromArgb(16, 124, 16))
                : new Palette(Color.White, Color.FromArgb(200, 255, 255, 255),
                    Color.FromArgb(255, 170, 80), Color.FromArgb(108, 203, 95));
        }
    }

    // ---------------------------------------------------------------- mouse: drag + tooltip

    private int Along(Point p) => _taskbar?.IsHorizontal != false ? p.X : p.Y;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || _taskbar is null)
            return;
        _dragging = true;
        _dragStartCursor = Along(Cursor.Position);
        _dragStartPos = _dragPos = Along(_position);
        _toolTip.Hide(this);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            _dragPos = _dragStartPos + Along(Cursor.Position) - _dragStartCursor;
            Render();
            return;
        }

        string? id = null;
        foreach (var zone in _hitZones)
        {
            if (zone.Rect.Contains(e.Location))
            {
                id = zone.Stat.Id;
                break;
            }
        }
        if (id != _toolTipId)
        {
            _toolTipId = id;
            RefreshToolTip();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging)
            return;
        _dragging = false;

        var tb = _taskbar;
        if (tb is null || Math.Abs(_dragPos - _dragStartPos) < 3)
            return;

        // Remember the offset from whichever end of the taskbar is closer, so the widget grows
        // away from that end when interfaces come and go.
        int length = tb.IsHorizontal ? _size.Width : _size.Height;
        int start = tb.IsHorizontal ? tb.Bounds.Left : tb.Bounds.Top;
        int end = tb.IsHorizontal ? tb.Bounds.Right : tb.Bounds.Bottom;
        int pos = Along(_position);
        if (pos + length / 2 < (start + end) / 2)
        {
            _settings.Anchor = AnchorSide.Near;
            _settings.AnchorOffset = pos - start;
        }
        else
        {
            _settings.Anchor = AnchorSide.Far;
            _settings.AnchorOffset = end - (pos + length);
        }
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _toolTipId = null;
        RefreshToolTip();
    }

    private void RefreshToolTip()
    {
        var stat = _items.FirstOrDefault(s => s.Id == _toolTipId);
        string? text = stat is null ? null : Describe(stat);
        if (_toolTip.GetToolTip(this) != (text ?? ""))
            _toolTip.SetToolTip(this, text);
    }

    private string Describe(InterfaceStat s)
    {
        var sb = new StringBuilder();
        sb.AppendLine(s.Name);
        sb.AppendLine(s.Description);
        sb.AppendLine($"Link: {SpeedFormatter.FormatLinkSpeed(s.LinkSpeed)}  ·  {s.Type}");
        foreach (var address in s.Addresses)
            sb.AppendLine($"IP: {address}");
        sb.AppendLine($"Upload: {SpeedFormatter.Format(s.UpBytesPerSec, _settings.UseBits)}  ·  " +
                      $"Download: {SpeedFormatter.Format(s.DownBytesPerSec, _settings.UseBits)}");
        sb.Append($"Sent: {SpeedFormatter.FormatBytes(s.TotalSent)}  ·  Received: {SpeedFormatter.FormatBytes(s.TotalReceived)}");
        return sb.ToString();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UnhookWinEvent(_foregroundHook);
            _obstacles.Dispose();
            _toolTip.Dispose();
            _valueFont?.Dispose();
            _nameFont?.Dispose();
            _leftFormat.Dispose();
            _rightFormat.Dispose();
        }
        base.Dispose(disposing);
    }
}
