// Generates the Microsoft Store listing screenshots (1920x1080 PNG) into
// packaging/Store/screenshots/. Draws a neutral desktop with NetMetter's genuine UI —
// same layout, colours and fonts as the running app — so the assets carry no private
// desktop content. Run: dotnet run --project tools/ShotGen [repoRoot]

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

const int W = 1920, H = 1080, TaskbarH = 60;

string root = args.Length > 0 ? Path.GetFullPath(args[0]) : FindRepoRoot();
string outDir = Path.Combine(root, "packaging", "Store", "screenshots");
Directory.CreateDirectory(outDir);

Adapter ethernet = new("Ethernet", "63.3 KB/s", "7.0 KB/s");
Adapter wifi = new("Wi-Fi", "1.5 KB/s", "1.2 KB/s");
Adapter[] one = [ethernet];
Adapter[] two = [ethernet, wifi];

Save("01-taskbar-single.png", g => { Desktop(g); Taskbar(g, one); });
Save("02-taskbar-multiple.png", g => { Desktop(g); Taskbar(g, two); });
Save("03-hover-details.png", g => { Desktop(g); float mx = Taskbar(g, two); Tooltip(g, mx); });
// Floating mode replaces the taskbar readout, so the taskbar shows no meter here.
Save("04-floating-window.png", g => { Desktop(g); Taskbar(g, []); FloatingPanel(g, two); });

Console.WriteLine($"Screenshots written to {outDir}");

void Save(string name, Action<Graphics> draw)
{
    using var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bmp))
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        draw(g);
    }
    bmp.Save(Path.Combine(outDir, name), ImageFormat.Png);
    Console.WriteLine($"  {name}");
}

// ---------------------------------------------------------------- desktop

void Desktop(Graphics g)
{
    using (var bg = new LinearGradientBrush(new Rectangle(0, 0, W, H),
        Color.FromArgb(7, 26, 47), Color.FromArgb(18, 74, 92), 55f))
        g.FillRectangle(bg, 0, 0, W, H);

    // Soft off-centre bloom.
    using var glow = new GraphicsPath();
    glow.AddEllipse(760, -260, 1500, 1200);
    using var bloom = new PathGradientBrush(glow)
    {
        CenterColor = Color.FromArgb(120, 60, 170, 190),
        SurroundColors = [Color.FromArgb(0, 60, 170, 190)],
    };
    g.FillPath(bloom, glow);
}

// Returns the meter's left x, so the tooltip can point at it.
float Taskbar(Graphics g, Adapter[] adapters)
{
    var bar = new Rectangle(0, H - TaskbarH, W, TaskbarH);
    using (var fill = new SolidBrush(Color.FromArgb(238, 22, 22, 26)))
        g.FillRectangle(fill, bar);
    using (var top = new Pen(Color.FromArgb(28, 255, 255, 255)))
        g.DrawLine(top, 0, bar.Top, W, bar.Top);

    float cy = H - TaskbarH / 2f;

    // Centre cluster: Start + a few app tiles.
    Color[] tiles =
    [
        Color.FromArgb(0, 120, 212), Color.FromArgb(16, 124, 16), Color.FromArgb(202, 80, 16),
        Color.FromArgb(92, 45, 145), Color.FromArgb(0, 153, 188), Color.FromArgb(120, 120, 128),
    ];
    const int icon = 34, gap = 16;
    int count = tiles.Length + 1;
    float groupW = count * icon + (count - 1) * gap;
    float x = (W - groupW) / 2f;

    DrawStart(g, x, cy - icon / 2f, icon);
    x += icon + gap;
    foreach (var c in tiles)
    {
        DrawTile(g, x, cy - icon / 2f, icon, c);
        x += icon + gap;
    }

    // Search pill, left of the cluster.
    DrawSearch(g, (W - groupW) / 2f - 210, cy);

    // Right cluster (from the right edge inward): clock, system glyphs, chevron, meter.
    using var glyphBrush = new SolidBrush(Color.FromArgb(220, 255, 255, 255));
    float rx = W - 20;

    rx -= DrawClock(g, rx, cy);
    rx -= 22;
    rx -= DrawBattery(g, rx, cy, glyphBrush);
    rx -= 20;
    rx -= DrawSpeaker(g, rx, cy, glyphBrush);
    rx -= 20;
    rx -= DrawWifi(g, rx, cy, glyphBrush);
    rx -= 22;
    rx -= DrawChevron(g, rx, cy, glyphBrush);
    rx -= 26;

    if (adapters.Length == 0)
        return rx;

    float meterW = MeasureMeter(g, adapters);
    float meterLeft = rx - meterW;
    DrawMeter(g, meterLeft, adapters, nameAlpha: 210);
    return meterLeft;
}

void DrawStart(Graphics g, float x, float y, int size)
{
    using var b = new SolidBrush(Color.FromArgb(235, 90, 165, 230));
    float s = size * 0.30f, gp = size * 0.10f, ox = x + size * 0.15f, oy = y + size * 0.15f;
    foreach (var (dx, dy) in new[] { (0f, 0f), (s + gp, 0f), (0f, s + gp), (s + gp, s + gp) })
        g.FillRectangle(b, ox + dx, oy + dy, s, s);
}

void DrawTile(Graphics g, float x, float y, int size, Color color)
{
    using var path = Rounded(new RectangleF(x, y, size, size), 8);
    using var b = new LinearGradientBrush(new RectangleF(x, y, size, size),
        Lighten(color, 0.12f), color, 90f);
    g.FillPath(b, path);
    // Faint inner mark so tiles do not read as flat blocks.
    using var mark = new SolidBrush(Color.FromArgb(60, 255, 255, 255));
    g.FillEllipse(mark, x + size * 0.34f, y + size * 0.34f, size * 0.32f, size * 0.32f);
}

void DrawSearch(Graphics g, float right, float cy)
{
    float w = 190, h = 34, x = right - w, y = cy - h / 2f;
    using var path = Rounded(new RectangleF(x, y, w, h), h / 2f);
    using var fill = new SolidBrush(Color.FromArgb(40, 255, 255, 255));
    using var border = new Pen(Color.FromArgb(50, 255, 255, 255));
    g.FillPath(fill, path);
    g.DrawPath(border, path);
    using var pen = new Pen(Color.FromArgb(200, 255, 255, 255), 2f);
    g.DrawEllipse(pen, x + 14, cy - 7, 12, 12);
    g.DrawLine(pen, x + 25, cy + 4, x + 30, cy + 9);
    using var f = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point);
    using var t = new SolidBrush(Color.FromArgb(180, 255, 255, 255));
    g.DrawString("Search", f, t, x + 40, cy - 10);
}

// ---------------------------------------------------------------- the meter (genuine layout)

const string ValueFont = "Segoe UI";
const string NameFont = "Segoe UI Semibold";

float MeasureMeter(Graphics g, Adapter[] adapters)
{
    using var vf = Px(ValueFont, 13.5f);
    float col = ColumnWidth(g, adapters, vf);
    return adapters.Length * col + (adapters.Length - 1) * 20;
}

float ColumnWidth(Graphics g, Adapter[] adapters, Font vf)
{
    float arrow = Text(g, "↓", vf);
    float widest = 0;
    foreach (var a in adapters)
        widest = Math.Max(widest, Math.Max(Text(g, a.Up, vf), Text(g, a.Down, vf)));
    return arrow + 8 + widest;
}

void DrawMeter(Graphics g, float x, Adapter[] adapters, int nameAlpha)
{
    using var vf = Px(ValueFont, 13.5f);
    using var nf = Px(NameFont, 13f);
    float col = ColumnWidth(g, adapters, vf);
    float lh = 16f;
    float top = H - TaskbarH + (TaskbarH - 3 * lh) / 2f;

    using var name = new SolidBrush(Color.FromArgb(nameAlpha, 255, 255, 255));
    using var value = new SolidBrush(Color.White);
    using var up = new SolidBrush(Color.FromArgb(255, 170, 80));
    using var down = new SolidBrush(Color.FromArgb(108, 203, 95));

    foreach (var a in adapters)
    {
        DrawLeft(g, a.Name, nf, name, x, top, col);
        Line(g, "↑", a.Up, vf, up, value, x, top + lh, col);
        Line(g, "↓", a.Down, vf, down, value, x, top + 2 * lh, col);
        x += col + 20;
    }
}

void Line(Graphics g, string arrow, string val, Font f, Brush arrowBrush, Brush valueBrush, float x, float y, float col)
{
    DrawLeft(g, arrow, f, arrowBrush, x, y, col);
    DrawRight(g, val, f, valueBrush, x, y, col);
}

// ---------------------------------------------------------------- tooltip + floating panel

void Tooltip(Graphics g, float meterLeft)
{
    string[] lines =
    [
        "Ethernet",
        "Realtek PCIe GbE Family Controller",
        "Link: 1 Gbps  ·  Ethernet",
        "IP: 192.168.1.42",
        "Upload: 63.3 KB/s  ·  Download: 7.0 KB/s",
        "Sent: 4.21 GB  ·  Received: 18.7 GB",
    ];
    using var f = Px(ValueFont, 15f);
    using var bf = Px(NameFont, 15f);

    float pad = 16, lh = 24;
    float w = 0;
    foreach (var l in lines)
        w = Math.Max(w, Text(g, l, f));
    w += pad * 2;
    float h = pad * 2 + lines.Length * lh;
    float x = Math.Max(24, meterLeft - 40);
    float y = H - TaskbarH - h - 16;

    Shadow(g, new RectangleF(x, y, w, h), 10, 26);
    using var path = Rounded(new RectangleF(x, y, w, h), 10);
    using var fill = new SolidBrush(Color.FromArgb(248, 43, 43, 43));
    using var border = new Pen(Color.FromArgb(70, 255, 255, 255));
    g.FillPath(fill, path);
    g.DrawPath(border, path);

    using var text = new SolidBrush(Color.FromArgb(235, 255, 255, 255));
    using var dim = new SolidBrush(Color.FromArgb(180, 255, 255, 255));
    for (int i = 0; i < lines.Length; i++)
        g.DrawString(lines[i], i == 0 ? bf : f, i == 1 ? dim : text, x + pad, y + pad + i * lh);
}

void FloatingPanel(Graphics g, Adapter[] adapters)
{
    using var vf = Px(ValueFont, 18f);
    using var nf = Px(NameFont, 17f);
    float col = 0, arrow = Text(g, "↓", vf);
    foreach (var a in adapters)
        col = Math.Max(col, arrow + 12 + Math.Max(Text(g, a.Up, vf), Text(g, a.Down, vf)));

    float pad = 22, gap = 26, lh = 24;
    float w = pad * 2 + adapters.Length * col + (adapters.Length - 1) * gap;
    float h = pad * 2 + 3 * lh;
    float x = 1360, y = 300;

    Shadow(g, new RectangleF(x, y, w, h), 16, 40);
    using var path = Rounded(new RectangleF(x, y, w, h), 14);
    using var fill = new SolidBrush(Color.FromArgb(242, 32, 32, 32));
    using var border = new Pen(Color.FromArgb(120, 255, 255, 255));
    g.FillPath(fill, path);
    g.DrawPath(border, path);

    using var name = new SolidBrush(Color.FromArgb(215, 255, 255, 255));
    using var value = new SolidBrush(Color.White);
    using var up = new SolidBrush(Color.FromArgb(255, 170, 80));
    using var down = new SolidBrush(Color.FromArgb(108, 203, 95));

    float cx = x + pad;
    foreach (var a in adapters)
    {
        DrawLeft(g, a.Name, nf, name, cx, y + pad, col);
        Line(g, "↑", a.Up, vf, up, value, cx, y + pad + lh, col);
        Line(g, "↓", a.Down, vf, down, value, cx, y + pad + 2 * lh, col);
        cx += col + gap;
    }
}

// ---------------------------------------------------------------- system tray glyphs

float DrawClock(Graphics g, float right, float cy)
{
    using var f = new Font("Segoe UI", 10.5f, FontStyle.Regular, GraphicsUnit.Point);
    using var b = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
    using var fmt = new StringFormat { Alignment = StringAlignment.Far };
    g.DrawString("10:53 AM", f, b, right, cy - 18, fmt);
    g.DrawString("9/14/2026", f, b, right, cy + 1, fmt);
    return 76;
}

float DrawBattery(Graphics g, float right, float cy, Brush b)
{
    float w = 26, h = 13, x = right - w, y = cy - h / 2f;
    using var pen = new Pen(b, 1.6f);
    g.DrawRectangle(pen, x, y, w - 3, h);
    g.FillRectangle(b, x + w - 3, y + 3, 2.5f, h - 6);
    g.FillRectangle(b, x + 2, y + 2, (w - 7) * 0.7f, h - 4);
    return w;
}

float DrawSpeaker(Graphics g, float right, float cy, Brush b)
{
    float x = right - 22;
    var body = new PointF[] { new(x, cy - 3), new(x + 6, cy - 3), new(x + 12, cy - 9), new(x + 12, cy + 9), new(x + 6, cy + 3), new(x, cy + 3) };
    g.FillPolygon(b, body);
    using var pen = new Pen(b, 1.6f);
    g.DrawArc(pen, x + 12, cy - 8, 10, 16, -60, 120);
    return 24;
}

float DrawWifi(Graphics g, float right, float cy, Brush b)
{
    float x = right - 20, cx = x + 10;
    using var pen = new Pen(b, 1.8f);
    g.DrawArc(pen, cx - 11, cy - 6, 22, 22, 200, 140);
    g.DrawArc(pen, cx - 6, cy - 1, 12, 14, 200, 140);
    g.FillEllipse(b, cx - 2, cy + 6, 4, 4);
    return 22;
}

float DrawChevron(Graphics g, float right, float cy, Brush b)
{
    float x = right - 14;
    using var pen = new Pen(b, 1.8f);
    g.DrawLines(pen, [new PointF(x, cy + 2), new PointF(x + 6, cy - 4), new PointF(x + 12, cy + 2)]);
    return 14;
}

// ---------------------------------------------------------------- helpers

Font Px(string family, float px) => new(family, px, FontStyle.Regular, GraphicsUnit.Pixel);

float Text(Graphics g, string s, Font f) =>
    g.MeasureString(s, f, PointF.Empty, StringFormat.GenericTypographic).Width;

void DrawLeft(Graphics g, string s, Font f, Brush b, float x, float y, float col)
{
    using var fmt = new StringFormat(StringFormat.GenericTypographic) { LineAlignment = StringAlignment.Center };
    g.DrawString(s, f, b, new RectangleF(x, y, col, 16), fmt);
}

void DrawRight(Graphics g, string s, Font f, Brush b, float x, float y, float col)
{
    using var fmt = new StringFormat(StringFormat.GenericTypographic)
    { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
    g.DrawString(s, f, b, new RectangleF(x, y, col, 16), fmt);
}

GraphicsPath Rounded(RectangleF r, float radius)
{
    float d = radius * 2;
    var p = new GraphicsPath();
    p.AddArc(r.Left, r.Top, d, d, 180, 90);
    p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
    p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
    p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
    p.CloseFigure();
    return p;
}

void Shadow(Graphics g, RectangleF r, float radius, int spread)
{
    for (int i = spread; i > 0; i -= 2)
    {
        using var path = Rounded(RectangleF.Inflate(r, i, i), radius + i);
        using var b = new SolidBrush(Color.FromArgb(Math.Max(2, 40 - i), 0, 0, 0));
        g.FillPath(b, path);
    }
}

Color Lighten(Color c, float amount) => Color.FromArgb(c.A,
    (int)Math.Min(255, c.R + 255 * amount),
    (int)Math.Min(255, c.G + 255 * amount),
    (int)Math.Min(255, c.B + 255 * amount));

static string FindRepoRoot()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "src", "NetMetter.csproj")))
            return dir.FullName;
    throw new InvalidOperationException("Could not find the repository root; pass it as the first argument.");
}

record Adapter(string Name, string Up, string Down);
