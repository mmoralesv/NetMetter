using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

namespace NetMetter;

/// <summary>Owns the sampling loop, the meter window, the tray icon and the shared context menu.</summary>
internal sealed class TrayApplication : ApplicationContext
{
    private static readonly int[] IntervalChoices = [500, 1000, 2000, 5000];

    private readonly AppSettings _settings;
    private readonly NetworkMonitor _monitor = new();
    private readonly MeterWindow _meter;
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly PeriodicTimer _timer;

    private ToolStripMenuItem _interfacesMenu = null!;
    private ToolStripMenuItem _startupItem = null!;
    private ToolStripMenuItem _taskbarModeItem = null!;
    private ToolStripMenuItem _floatingModeItem = null!;
    private IReadOnlyList<InterfaceStat> _latest = [];
    private bool _exiting;

    public TrayApplication(AppSettings settings)
    {
        _settings = settings;
        _meter = new MeterWindow(_settings);
        _meter.PositionChanged += (_, _) => _settings.Save();

        _menu = BuildMenu();
        _meter.ContextMenuStrip = _menu;

        _icon = AppIcon.Create();
        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "NetMetter",
            ContextMenuStrip = _menu,
            Visible = true,
        };

        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Math.Max(250, _settings.IntervalMs)));
        RunSamplingLoop();

        SystemEvents.DisplaySettingsChanged += OnSystemChanged;
        SystemEvents.UserPreferenceChanged += OnSystemChanged;
    }

    private void OnSystemChanged(object? sender, EventArgs e) => _meter.Render();

    /// <summary>
    /// Samples on a worker thread (the adapter APIs can stall for seconds while a network is
    /// connecting) and applies the result back on the UI thread. Ends when the timer is disposed.
    /// </summary>
    private async void RunSamplingLoop()
    {
        do
        {
            try
            {
                // The first sample only primes the counters, so it renders zeros.
                var stats = await Task.Run(_monitor.Sample);
                if (_exiting)
                    return;
                _latest = stats;
                var visible = stats.Where(_settings.IsVisible).ToList();
                _meter.UpdateItems(visible);
                _notifyIcon.Text = BuildTrayText(visible);
            }
            catch (Exception ex)
            {
                // One bad sample (adapter removed mid-read, display change during paint) must not
                // end the loop; the next tick usually succeeds.
                AppLog.Error("Sampling tick", ex);
            }
        }
        while (await _timer.WaitForNextTickAsync());
    }

    private string BuildTrayText(IReadOnlyList<InterfaceStat> visible)
    {
        const int maxLength = 127; // Shell_NotifyIcon limit
        bool bits = _settings.UseBits;
        var sb = new StringBuilder("NetMetter");
        if (visible.Count == 0)
            return "NetMetter — no network";

        foreach (var s in visible)
        {
            string line = $"\n{s.Name}: ↑ {SpeedFormatter.Format(s.UpBytesPerSec, bits)}  ↓ {SpeedFormatter.Format(s.DownBytesPerSec, bits)}";
            if (sb.Length + line.Length > maxLength)
                break;
            sb.Append(line);
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- menu

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        _interfacesMenu = new ToolStripMenuItem("Interfaces");
        _interfacesMenu.DropDownItems.Add(new ToolStripMenuItem("(loading)") { Enabled = false });
        _interfacesMenu.DropDownOpening += (_, _) => PopulateInterfacesMenu();
        // Keep the submenu open while toggling several interfaces.
        _interfacesMenu.DropDown.Closing += (_, e) =>
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
                e.Cancel = true;
        };

        var display = new ToolStripMenuItem("Show meter");
        _taskbarModeItem = new ToolStripMenuItem("On the taskbar", null, (_, _) => SetMode(DisplayMode.Taskbar));
        _floatingModeItem = new ToolStripMenuItem("In a floating window", null, (_, _) => SetMode(DisplayMode.Floating));
        display.DropDownItems.AddRange([_taskbarModeItem, _floatingModeItem]);

        var showNames = new ToolStripMenuItem("Show interface names") { Checked = _settings.ShowNames, CheckOnClick = true };
        showNames.CheckedChanged += (_, _) =>
        {
            _settings.ShowNames = showNames.Checked;
            SaveAndRender();
        };

        var units = new ToolStripMenuItem("Units");
        var bytesItem = new ToolStripMenuItem("Bytes per second (KB/s, MB/s)");
        var bitsItem = new ToolStripMenuItem("Bits per second (Kbps, Mbps)");
        void SetUnits(bool useBits)
        {
            _settings.UseBits = useBits;
            bytesItem.Checked = !useBits;
            bitsItem.Checked = useBits;
            SaveAndRender();
        }
        bytesItem.Click += (_, _) => SetUnits(false);
        bitsItem.Click += (_, _) => SetUnits(true);
        bytesItem.Checked = !_settings.UseBits;
        bitsItem.Checked = _settings.UseBits;
        units.DropDownItems.AddRange([bytesItem, bitsItem]);

        var interval = new ToolStripMenuItem("Update interval");
        foreach (int ms in IntervalChoices)
        {
            var item = new ToolStripMenuItem(ms < 1000 ? $"{ms} ms" : $"{ms / 1000} s") { Tag = ms, Checked = ms == _settings.IntervalMs };
            item.Click += (_, _) =>
            {
                _settings.IntervalMs = ms;
                _timer.Period = TimeSpan.FromMilliseconds(ms);
                foreach (ToolStripMenuItem other in interval.DropDownItems)
                    other.Checked = (int)other.Tag! == ms;
                _settings.Save();
            };
            interval.DropDownItems.Add(item);
        }

        var resetPosition = new ToolStripMenuItem("Reset position", null, (_, _) =>
        {
            _meter.ResetPosition();
            _settings.Save();
        });

        _startupItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleStartup());

        var networkSettings = new ToolStripMenuItem("Network settings…", null, (_, _) =>
            Open("ms-settings:network"));

        var about = new ToolStripMenuItem("About NetMetter…", null, (_, _) => ShowAbout());

        var exit = new ToolStripMenuItem("Exit", null, (_, _) => ExitThread());

        menu.Items.AddRange([
            _interfacesMenu,
            display,
            showNames,
            units,
            interval,
            new ToolStripSeparator(),
            resetPosition,
            _startupItem,
            networkSettings,
            new ToolStripSeparator(),
            about,
            exit,
        ]);
        menu.Opening += (_, _) =>
        {
            _taskbarModeItem.Checked = _settings.Mode == DisplayMode.Taskbar;
            _floatingModeItem.Checked = _settings.Mode == DisplayMode.Floating;
            RefreshStartupItem();
        };
        return menu;
    }

    private void SetMode(DisplayMode mode)
    {
        if (_settings.Mode == mode)
            return;
        _settings.Mode = mode;
        _settings.Save();
        _meter.Render();
    }

    private async void RefreshStartupItem()
    {
        try
        {
            _startupItem.Checked = StartupRegistration.IsOn(await StartupRegistration.GetStateAsync());
        }
        catch (Exception ex)
        {
            AppLog.Error("Reading start-up state", ex);
        }
    }

    private async void ToggleStartup()
    {
        try
        {
            bool turnOn = !StartupRegistration.IsOn(await StartupRegistration.GetStateAsync());
            var result = await StartupRegistration.SetEnabledAsync(turnOn);
            _startupItem.Checked = StartupRegistration.IsOn(result);

            if (turnOn && result == StartupState.DisabledByUser)
                OfferStartupSettings("Start-up for NetMetter is turned off in Windows Settings, so only you can turn it back on.");
            else if (result is StartupState.DisabledByPolicy or StartupState.EnabledByPolicy)
                MessageBox.Show("A policy on this PC controls whether NetMetter starts with Windows.",
                    "NetMetter", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            AppLog.Error("Changing start-up state", ex);
        }
    }

    private static void OfferStartupSettings(string message)
    {
        var answer = MessageBox.Show($"{message}\n\nOpen Startup Apps settings now?",
            "NetMetter", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (answer == DialogResult.Yes)
            Open("ms-settings:startupapps");
    }

    private void ShowAbout() =>
        MessageBox.Show(
            $"NetMetter {AppEnvironment.Version}\n\n" +
            "Live upload and download speed for each connected network interface.\n\n" +
            $"{AppEnvironment.PrivacyStatement}\n\n" +
            $"Log file: {AppLog.FilePath}",
            "About NetMetter", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Error($"Opening {target}", ex);
        }
    }

    private void PopulateInterfacesMenu()
    {
        _interfacesMenu.DropDownItems.Clear();

        if (_latest.Count == 0)
        {
            _interfacesMenu.DropDownItems.Add(new ToolStripMenuItem("No connected interfaces") { Enabled = false });
            return;
        }

        foreach (var stat in _latest)
        {
            var item = new ToolStripMenuItem($"{stat.Name}  —  {stat.Description}")
            {
                Checked = _settings.IsVisible(stat),
                ToolTipText = string.Join(", ", stat.Addresses),
            };
            item.Click += (_, _) =>
            {
                bool show = !_settings.IsVisible(stat);
                _settings.InterfaceVisibility[stat.Id] = show;
                item.Checked = show;
                SaveAndRender();
            };
            _interfacesMenu.DropDownItems.Add(item);
        }

        _interfacesMenu.DropDownItems.Add(new ToolStripSeparator());
        _interfacesMenu.DropDownItems.Add(new ToolStripMenuItem("Automatic (only interfaces with a gateway)", null, (_, _) =>
        {
            _settings.InterfaceVisibility.Clear();
            SaveAndRender();
            PopulateInterfacesMenu();
        }));
    }

    private void SaveAndRender()
    {
        _settings.Save();
        _meter.UpdateItems(_latest.Where(_settings.IsVisible).ToList());
    }

    protected override void ExitThreadCore()
    {
        _exiting = true;
        _timer.Dispose();
        _notifyIcon.Visible = false;
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.DisplaySettingsChanged -= OnSystemChanged;
            SystemEvents.UserPreferenceChanged -= OnSystemChanged;
            _timer.Dispose();
            _notifyIcon.Dispose();
            _icon.Dispose();
            _menu.Dispose();
            _meter.Dispose();
            _monitor.Dispose();
        }
        base.Dispose(disposing);
    }
}
