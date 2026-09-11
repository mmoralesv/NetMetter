namespace NetMetter;

internal static class SpeedFormatter
{
    private static readonly string[] ByteUnits = ["KB/s", "MB/s", "GB/s"];
    private static readonly string[] BitUnits = ["Kbps", "Mbps", "Gbps"];

    /// <summary>Widest string <see cref="Format"/> can produce; used to size columns so they don't jitter.</summary>
    public static string WidestSample(bool bits) => bits ? "999.9 Mbps" : "999.9 MB/s";

    public static string Format(double bytesPerSec, bool bits)
    {
        var (value, step, units) = bits
            ? (bytesPerSec * 8 / 1000, 1000.0, BitUnits)
            : (bytesPerSec / 1024, 1024.0, ByteUnits);

        int unit = 0;
        while (value >= 999.95 && unit < units.Length - 1)
        {
            value /= step;
            unit++;
        }
        return $"{value.ToString(value < 100 ? "0.0" : "0")} {units[unit]}";
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }

    public static string FormatLinkSpeed(long bitsPerSec) => bitsPerSec switch
    {
        <= 0 => "unknown",
        >= 1_000_000_000 => $"{bitsPerSec / 1e9:0.##} Gbps",
        >= 1_000_000 => $"{bitsPerSec / 1e6:0.#} Mbps",
        _ => $"{bitsPerSec / 1e3:0} Kbps",
    };
}
