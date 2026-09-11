namespace NetMetter;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, @"Local\NetMetter.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
            return;

        // A meter is not worth losing: log failures and keep the tray icon alive where we can.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => AppLog.Error("Unhandled UI exception", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                AppLog.Error("Unhandled exception", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Error("Unobserved task exception", e.Exception);
            e.SetObserved();
        };

        ApplicationConfiguration.Initialize();

        var settings = AppSettings.Load();
        if (!settings.FirstRunCompleted && !RunFirstTimeSetup(settings))
            return;

        Application.Run(new TrayApplication(settings));
    }

    /// <summary>Asks where the meter should live before drawing anything. False means the user quit.</summary>
    private static bool RunFirstTimeSetup(AppSettings settings)
    {
        using var dialog = new WelcomeDialog(settings.Mode);
        var choice = dialog.ShowDialog();
        AppLog.Info($"First run: {choice}, mode {dialog.SelectedMode}, startup {dialog.StartWithWindows}");
        if (choice != DialogResult.OK)
            return false;

        settings.Mode = dialog.SelectedMode;
        settings.FirstRunCompleted = true;
        settings.Save();

        if (dialog.StartWithWindows)
            EnableStartup();
        return true;
    }

    private static async void EnableStartup()
    {
        try
        {
            await StartupRegistration.SetEnabledAsync(true);
        }
        catch (Exception ex)
        {
            AppLog.Error("Enabling start-up from the welcome dialog", ex);
        }
    }
}
