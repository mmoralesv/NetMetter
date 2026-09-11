using System.Text;

namespace NetMetter;

/// <summary>
/// Minimal append-only log in %LOCALAPPDATA%\NetMetter (redirected into the package's private
/// storage when installed from the Store). Rolls over to a single .old file at 1 MB.
/// </summary>
internal static class AppLog
{
    private const long MaxBytes = 1024 * 1024;
    private static readonly object Gate = new();

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetMetter", "netmetter.log");

    public static void Error(string context, Exception ex) => Write($"ERROR {context}: {ex}");

    public static void Info(string message) => Write($"INFO  {message}");

    private static void Write(string line)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes)
                    File.Move(FilePath, FilePath + ".old", overwrite: true);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}{Environment.NewLine}", Encoding.UTF8);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never take the app down.
        }
    }
}
