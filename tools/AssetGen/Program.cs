// Regenerates every icon asset from src/AppIcon.cs:
//   src/Resources/NetMetter.ico       executable icon
//   packaging/Assets/*.png            MSIX logos (scale and target-size variants, resolved via resources.pri)
//   packaging/Store/AppTile300.png    Store listing "1:1 app tile icon"
//
// Usage: dotnet run --project tools/AssetGen [repoRoot]

using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using NetMetter;

string root = args.Length > 0 ? Path.GetFullPath(args[0]) : FindRepoRoot();
string assets = Path.Combine(root, "packaging", "Assets");
string store = Path.Combine(root, "packaging", "Store");
Directory.CreateDirectory(assets);
Directory.CreateDirectory(store);

int[] scales = [100, 125, 150, 200, 400];

WriteIco(Path.Combine(root, "src", "Resources", "NetMetter.ico"), [16, 20, 24, 32, 40, 48, 64, 256]);

foreach (int scale in scales)
{
    // Logos that show the icon edge to edge.
    SavePng(Canvas(Scaled(44, scale), Scaled(44, scale), 1f), assets, $"Square44x44Logo.scale-{scale}.png");
    SavePng(Canvas(Scaled(50, scale), Scaled(50, scale), 1f), assets, $"StoreLogo.scale-{scale}.png");
    // Start/tile logos keep some breathing room around the icon.
    SavePng(Canvas(Scaled(150, scale), Scaled(150, scale), 0.6f), assets, $"Square150x150Logo.scale-{scale}.png");
    SavePng(Canvas(Scaled(310, scale), Scaled(150, scale), 0.6f), assets, $"Wide310x150Logo.scale-{scale}.png");
}

// Taskbar, Start list and Explorer pick these exact pixel sizes; "unplated" means no accent-colour plate behind the icon.
foreach (int size in new[] { 16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256 })
{
    SavePng(Canvas(size, size, 1f), assets, $"Square44x44Logo.targetsize-{size}.png");
    SavePng(Canvas(size, size, 1f), assets, $"Square44x44Logo.targetsize-{size}_altform-unplated.png");
    SavePng(Canvas(size, size, 1f), assets, $"Square44x44Logo.targetsize-{size}_altform-lightunplated.png");
}

SavePng(Canvas(300, 300, 1f), store, "AppTile300.png");

Console.WriteLine($"Assets written under {root}");

static int Scaled(int size, int scale) => (int)Math.Round(size * scale / 100.0);

/// <summary>A transparent canvas with the icon centred, sized to <paramref name="fraction"/> of the shorter side.</summary>
static Bitmap Canvas(int width, int height, float fraction)
{
    int iconSize = Math.Max(1, (int)Math.Round(Math.Min(width, height) * fraction));
    var canvas = new Bitmap(width, height, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(canvas);
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    using var icon = AppIcon.Render(iconSize);
    g.DrawImageUnscaled(icon, (width - iconSize) / 2, (height - iconSize) / 2);
    return canvas;
}

static void SavePng(Bitmap bitmap, string directory, string name)
{
    using (bitmap)
        bitmap.Save(Path.Combine(directory, name), ImageFormat.Png);
}

/// <summary>Writes a multi-resolution .ico whose frames are PNG-compressed (supported since Windows Vista).</summary>
static void WriteIco(string path, int[] sizes)
{
    var frames = sizes.Select(size =>
    {
        using var bmp = AppIcon.Render(size);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }).ToArray();

    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    using var writer = new BinaryWriter(File.Create(path));
    writer.Write((ushort)0);            // reserved
    writer.Write((ushort)1);            // type: icon
    writer.Write((ushort)sizes.Length);

    int offset = 6 + 16 * sizes.Length;
    for (int i = 0; i < sizes.Length; i++)
    {
        byte dimension = (byte)(sizes[i] >= 256 ? 0 : sizes[i]); // 0 means 256
        writer.Write(dimension);
        writer.Write(dimension);
        writer.Write((byte)0);          // palette size
        writer.Write((byte)0);          // reserved
        writer.Write((ushort)1);        // colour planes
        writer.Write((ushort)32);       // bits per pixel
        writer.Write(frames[i].Length);
        writer.Write(offset);
        offset += frames[i].Length;
    }
    foreach (var frame in frames)
        writer.Write(frame);
}

static string FindRepoRoot()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "src", "NetMetter.csproj")))
            return dir.FullName;
    }
    throw new InvalidOperationException("Could not find the repository root; pass it as the first argument.");
}
