using System.Reflection;

namespace NetMetter;

internal static class AppEnvironment
{
    /// <summary>True when running from an MSIX package (e.g. installed from the Microsoft Store).</summary>
    public static bool IsPackaged { get; } = DetectPackage();

    /// <summary>MAJOR.MINOR.PATCH of this build.</summary>
    public static string Version { get; } =
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>
    /// Shown from the menu and the welcome dialog. Keep in sync with docs/PRIVACY.md, which is the
    /// hosted copy the Store listing links to.
    /// </summary>
    public const string PrivacyStatement =
        "NetMetter reads network statistics (adapter names, IP addresses and byte counters) on this PC " +
        "to display them. It does not collect, store or send any of this information anywhere. " +
        "The only thing it saves is its own settings, on this PC.";

    private static bool DetectPackage()
    {
        uint length = 0;
        return NativeMethods.GetCurrentPackageFullName(ref length, null) != NativeMethods.APPMODEL_ERROR_NO_PACKAGE;
    }
}
