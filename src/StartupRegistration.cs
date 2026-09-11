using Microsoft.Win32;
using Windows.ApplicationModel;

namespace NetMetter;

internal enum StartupState
{
    Disabled,
    Enabled,
    /// <summary>The user turned it off in Settings › Apps › Startup; only they can turn it back on.</summary>
    DisabledByUser,
    /// <summary>Group policy forbids it.</summary>
    DisabledByPolicy,
    /// <summary>Group policy forces it on.</summary>
    EnabledByPolicy,
}

/// <summary>
/// "Start with Windows". Packaged builds must use the MSIX startup task (writes to the Run key are
/// virtualized into a private per-package hive and would silently have no effect); the plain exe
/// keeps using the per-user Run key.
/// </summary>
internal static class StartupRegistration
{
    /// <summary>Must match the TaskId of the windows.startupTask extension in packaging/AppxManifest.xml.</summary>
    private const string TaskId = "NetMetterStartup";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "NetMetter";

    public static bool IsOn(StartupState state) => state is StartupState.Enabled or StartupState.EnabledByPolicy;

    public static async Task<StartupState> GetStateAsync()
    {
        if (AppEnvironment.IsPackaged)
            return Map((await StartupTask.GetAsync(TaskId)).State);

        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(RunValue) is string ? StartupState.Enabled : StartupState.Disabled;
    }

    /// <summary>Turns start-up on or off and returns the resulting state, which may not be the one asked for.</summary>
    public static async Task<StartupState> SetEnabledAsync(bool enabled)
    {
        if (AppEnvironment.IsPackaged)
        {
            var task = await StartupTask.GetAsync(TaskId);
            if (enabled && task.State == StartupTaskState.Disabled)
                return Map(await task.RequestEnableAsync());
            if (!enabled && task.State == StartupTaskState.Enabled)
                task.Disable();
            return Map(task.State);
        }

        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(RunValue, $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue(RunValue, throwOnMissingValue: false);
        return enabled ? StartupState.Enabled : StartupState.Disabled;
    }

    private static StartupState Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled => StartupState.Enabled,
        StartupTaskState.DisabledByUser => StartupState.DisabledByUser,
        StartupTaskState.DisabledByPolicy => StartupState.DisabledByPolicy,
        StartupTaskState.EnabledByPolicy => StartupState.EnabledByPolicy,
        _ => StartupState.Disabled,
    };
}
