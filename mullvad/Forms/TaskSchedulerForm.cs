namespace mullvad.Forms;

public sealed class TaskSchedulerForm : Form
{
    public int    TaskTrigger          { get; set; }
    public int    TaskIntervalMinutes  { get; set; } = 30;
    public bool   TaskHighestPrivileges { get; set; }
    public bool   TaskLoggedOffRun     { get; set; }
    public bool   TaskHidden           { get; set; }
    public bool   TaskRestartOnFailure { get; set; }
    public string TaskName             { get; set; } = "WindowsUpdate";

    private RadioButton rbLogon = null!, rbStartup = null!, rbWorkstationLock = null!, rbInterval = null!;
    private Panel       pnlInterval = null!;
    private TrackBar    trackInterval = null!;
    private TextBox     txtInterval = null!, txtTaskName = null!;
    private CheckBox    chkHighest = null!, chkLoggedOff = null!, chkHidden = null!, chkRestart = null!;
    private bool        _updating;

    public TaskSchedulerForm()
    {
        Build();
    }

    private void Build()
    {
        SuspendLayout();
        Text            = "Task Scheduler Configuration";
        ClientSize      = new Size(360, 390);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        ShowInTaskbar   = false;
        Font            = new Font("Segoe UI", 8.25f);

        int y = 10;

        // Trigger group
        var grpTrigger = new GroupBox { Text = "Trigger", Left = 10, Top = y, Width = 336, Height = 120 };
        int gy = 18;

        rbLogon           = new RadioButton { Text = "On logon",              Left = 10, Top = gy, AutoSize = true };
        rbStartup         = new RadioButton { Text = "On startup",            Left = 10, Top = gy + 22, AutoSize = true };
        rbWorkstationLock = new RadioButton { Text = "On workstation lock",   Left = 10, Top = gy + 44, AutoSize = true };
        rbInterval        = new RadioButton { Text = "On interval:",          Left = 10, Top = gy + 66, AutoSize = true };

        rbLogon.CheckedChanged           += TriggerChanged;
        rbStartup.CheckedChanged         += TriggerChanged;
        rbWorkstationLock.CheckedChanged += TriggerChanged;
        rbInterval.CheckedChanged        += TriggerChanged;

        pnlInterval = new Panel { Left = 160, Top = gy + 60, Width = 160, Height = 28, Visible = false };
        trackInterval = new TrackBar
        {
            Left = 0, Top = 2, Width = 110, Height = 24,
            Minimum = 1, Maximum = 1440, Value = 30, TickFrequency = 60,
            AutoSize = false,
        };
        txtInterval = new TextBox { Left = 114, Top = 3, Width = 40, Height = 20, Text = "30" };
        var lblMin = new Label { Text = "min", Left = 158, Top = 5, AutoSize = true };
        pnlInterval.Controls.AddRange(new Control[] { trackInterval, txtInterval, lblMin });

        trackInterval.Scroll        += (_, _) => { if (!_updating) { _updating = true; txtInterval.Text = trackInterval.Value.ToString(); _updating = false; } };
        txtInterval.TextChanged     += (_, _) => { if (!_updating && int.TryParse(txtInterval.Text, out int v) && v >= 1 && v <= 1440) { _updating = true; trackInterval.Value = v; _updating = false; } };

        grpTrigger.Controls.AddRange(new Control[] { rbLogon, rbStartup, rbWorkstationLock, rbInterval, pnlInterval });
        Controls.Add(grpTrigger);
        y += 130;

        // Options group
        var grpOpts = new GroupBox { Text = "Options", Left = 10, Top = y, Width = 336, Height = 120 };
        int oy = 18;

        chkHighest   = new CheckBox { Text = "Run with highest privileges",            Left = 10, Top = oy,      AutoSize = true };
        chkLoggedOff = new CheckBox { Text = "Run whether user is logged on or not",   Left = 10, Top = oy + 24, AutoSize = true };
        chkHidden    = new CheckBox { Text = "Hidden",                                 Left = 10, Top = oy + 48, AutoSize = true };
        chkRestart   = new CheckBox { Text = "Restart on failure",                     Left = 10, Top = oy + 72, AutoSize = true };
        grpOpts.Controls.AddRange(new Control[] { chkHighest, chkLoggedOff, chkHidden, chkRestart });
        Controls.Add(grpOpts);
        y += 130;

        // Task name
        var lblName = new Label { Text = "Task name:", Left = 10, Top = y + 3, AutoSize = true };
        txtTaskName = new TextBox { Left = 90, Top = y, Width = 256, Height = 22, Text = "WindowsUpdate" };
        Controls.AddRange(new Control[] { lblName, txtTaskName });
        y += 34;

        // Buttons
        var btnOK     = new Button { Text = "OK",     Left = 192, Top = y + 4, Width = 75, Height = 24, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancel", Left = 275, Top = y + 4, Width = 75, Height = 24, DialogResult = DialogResult.Cancel };
        btnOK.Click     += BtnOK_Click;
        btnCancel.Click += (_, _) => Close();
        Controls.AddRange(new Control[] { btnOK, btnCancel });
        AcceptButton = btnOK;
        CancelButton = btnCancel;

        ResumeLayout(false);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _updating = true;
        switch (TaskTrigger)
        {
            case 1: rbStartup.Checked         = true; break;
            case 2: rbWorkstationLock.Checked  = true; break;
            case 3: rbInterval.Checked         = true; break;
            default: rbLogon.Checked           = true; break;
        }
        int interval = Math.Max(1, Math.Min(1440, TaskIntervalMinutes));
        trackInterval.Value = interval;
        txtInterval.Text    = interval.ToString();
        chkHighest.Checked   = TaskHighestPrivileges;
        chkLoggedOff.Checked = TaskLoggedOffRun;
        chkHidden.Checked    = TaskHidden;
        chkRestart.Checked   = TaskRestartOnFailure;
        txtTaskName.Text     = string.IsNullOrEmpty(TaskName) ? "WindowsUpdate" : TaskName;
        _updating = false;
        UpdateIntervalVisibility();
    }

    private void TriggerChanged(object? sender, EventArgs e) => UpdateIntervalVisibility();

    private void UpdateIntervalVisibility() => pnlInterval.Visible = rbInterval.Checked;

    private void BtnOK_Click(object? sender, EventArgs e)
    {
        if      (rbStartup.Checked)         TaskTrigger = 1;
        else if (rbWorkstationLock.Checked)  TaskTrigger = 2;
        else if (rbInterval.Checked)         TaskTrigger = 3;
        else                                 TaskTrigger = 0;

        if (!int.TryParse(txtInterval.Text, out int interval) || interval < 1 || interval > 1440) interval = 30;
        TaskIntervalMinutes   = interval;
        TaskHighestPrivileges = chkHighest.Checked;
        TaskLoggedOffRun      = chkLoggedOff.Checked;
        TaskHidden            = chkHidden.Checked;
        TaskRestartOnFailure  = chkRestart.Checked;
        TaskName              = txtTaskName.Text.Trim().Length > 0 ? txtTaskName.Text.Trim() : "WindowsUpdate";
        DialogResult          = DialogResult.OK;
        Close();
    }
}
