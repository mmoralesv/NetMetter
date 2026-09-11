using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace NetMetter;

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

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public bool ShowNames { get; set; } = true;
    public bool UseBits { get; set; }
    public int IntervalMs { get; set; } = 1000;
    public AnchorSide Anchor { get; set; } = AnchorSide.Auto;
    public int AnchorOffset { get; set; }

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
            // Corrupt or unreadable settings: start over with defaults.
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
            // Not fatal; settings just won't persist.
        }
    }

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "NetMetter";

    public static bool StartWithWindows
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunValue) is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value)
                key.SetValue(RunValue, $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue(RunValue, throwOnMissingValue: false);
        }
    }
}
