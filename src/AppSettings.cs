using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetMetter;

internal enum DisplayMode
{
    /// <summary>A readout drawn on top of the taskbar, next to the notification area.</summary>
    Taskbar,
    /// <summary>A small always-on-top window the user can place anywhere.</summary>
    Floating,
}

internal enum AnchorSide
{
    /// <summary>Position is chosen automatically (next to the notification area).</summary>
    Auto,
    /// <summary>Offset measured from the left (or top) end of the taskbar.</summary>
    Near,
    /// <summary>Offset measured from the right (or bottom) end of the taskbar.</summary>
    Far,
}

internal sealed class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetMetter", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>False until the user has confirmed the welcome dialog (their consent to the overlay).</summary>
    public bool FirstRunCompleted { get; set; }
    public DisplayMode Mode { get; set; } = DisplayMode.Taskbar;
    public bool ShowNames { get; set; } = true;
    public bool UseBits { get; set; }
    public int IntervalMs { get; set; } = 1000;

    public AnchorSide Anchor { get; set; } = AnchorSide.Auto;
    public int AnchorOffset { get; set; }

    /// <summary>Top-left of the floating window in screen pixels; null until the user moves it.</summary>
    public int? FloatingLeft { get; set; }
    public int? FloatingTop { get; set; }

    /// <summary>
    /// Per-interface overrides keyed by adapter id. Interfaces not listed here fall back to the
    /// default rule: shown if they have a default gateway (i.e. they actually reach a network).
    /// </summary>
    public Dictionary<string, bool> InterfaceVisibility { get; set; } = new();

    public bool IsVisible(InterfaceStat s) =>
        InterfaceVisibility.TryGetValue(s.Id, out var v) ? v : s.HasGateway;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            AppLog.Error("Loading settings; starting with defaults", ex);
        }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Saving settings", ex);
        }
    }
}
