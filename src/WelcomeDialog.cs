namespace NetMetter;

/// <summary>
/// Shown once, before anything is drawn on screen. The taskbar readout is an overlay on part of
/// the Windows shell, so the user picks where the meter lives and whether it starts with Windows
/// instead of finding it already there.
/// </summary>
internal sealed class WelcomeDialog : Form
{
    private readonly RadioButton _taskbarOption;
    private readonly RadioButton _floatingOption;
    private readonly CheckBox _startWithWindows;

    public DisplayMode SelectedMode => _taskbarOption.Checked ? DisplayMode.Taskbar : DisplayMode.Floating;
    public bool StartWithWindows => _startWithWindows.Checked;

    public WelcomeDialog(DisplayMode initialMode)
    {
        Text = "Welcome to NetMetter";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 9f);
        Icon = AppIcon.Create();
        Padding = new Padding(20, 18, 20, 14);

        var heading = new Label
        {
            Text = "Where should NetMetter show your network speed?",
            Font = new Font("Segoe UI", 12f, FontStyle.Regular),
            AutoSize = true,
            MaximumSize = new Size(430, 0),
            Margin = new Padding(0, 0, 0, 10),
        };

        _taskbarOption = new RadioButton
        {
            Text = "On the taskbar",
            Checked = initialMode == DisplayMode.Taskbar,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0),
        };
        _floatingOption = new RadioButton
        {
            Text = "In a floating window",
            Checked = initialMode == DisplayMode.Floating,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0),
        };

        _startWithWindows = new CheckBox
        {
            Text = "Start NetMetter when I sign in to Windows",
            AutoSize = true,
            Margin = new Padding(0, 16, 0, 0),
        };

        var privacy = new Label
        {
            Text = AppEnvironment.PrivacyStatement,
            AutoSize = true,
            MaximumSize = new Size(430, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 16, 0, 0),
        };

        var start = new Button { Text = "Start NetMetter", DialogResult = DialogResult.OK, AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
        var quit = new Button { Text = "Quit", DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 18, 0, 0),
        };
        buttons.Controls.AddRange([start, quit]);

        var layout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Dock = DockStyle.Fill,
        };
        layout.Controls.AddRange([
            heading,
            _taskbarOption,
            Hint("A small readout drawn on top of the taskbar, next to the clock. Drag it to move it."),
            _floatingOption,
            Hint("A small window that stays on top of other windows, anywhere you put it."),
            _startWithWindows,
            privacy,
            buttons,
        ]);

        Controls.Add(layout);
        AcceptButton = start;
        CancelButton = quit;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Launched at sign-in or from a script, the app may not hold the foreground, and this
        // dialog would wait unseen behind other windows.
        TopMost = true;
        TopMost = false;
        Activate();
    }

    private static Label Hint(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(410, 0),
        ForeColor = SystemColors.GrayText,
        Margin = new Padding(20, 2, 0, 0),
    };
}
