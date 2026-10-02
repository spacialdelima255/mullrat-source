using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms;

public sealed class RemoteShellForm : Form
{
    private const string ModuleFile = "mullvad.Module.RemoteShell";
    private const string ModuleId   = "mullvad.remoteshell";

    private readonly ClientHandler _handler;
    private readonly string        _initialDir;
    private ModuleContext?         _ctx;
    private bool                   _running;

    // ── Controls ─────────────────────────────────────────────────────────────
    private ToolStrip         toolbar      = null!;
    private ToolStripLabel    lblShell     = null!;
    private ToolStripComboBox cmbShell     = null!;
    private ToolStripButton   btnRestart   = null!;
    private RichTextBox       output       = null!;
    private Panel             inputPanel   = null!;
    private TextBox           inputBox     = null!;
    private Button            btnSend      = null!;
    private Panel             loadingPanel = null!;
    private Label             loadingLabel = null!;

    public RemoteShellForm(ClientHandler handler, string initialDir = "")
    {
        _handler    = handler;
        _initialDir = initialDir;
        Build();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Form construction
    // ─────────────────────────────────────────────────────────────────────────
    private void Build()
    {
        SuspendLayout();

        Text          = $"Remote Shell  —  {_handler.Info.Computer}";
        ClientSize    = new Size(820, 500);
        Font          = new Font("Segoe UI", 9F);
        MinimumSize   = new Size(500, 340);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = true;

        // Form icon
        var ico = IconLoader.Load("terminal.png") as Bitmap;
        if (ico is not null) try { Icon = Icon.FromHandle(ico.GetHicon()); } catch { }

        // ── Toolbar ───────────────────────────────────────────────────────────
        toolbar  = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        lblShell = new ToolStripLabel("Shell: ");

        cmbShell = new ToolStripComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            AutoSize      = false,
            Width         = 108,
        };
        cmbShell.Items.AddRange(new object[] { "CMD", "PowerShell" });
        cmbShell.SelectedIndex = 0;

        btnRestart = new ToolStripButton("Restart")
        {
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
            Image        = IconLoader.Load("refresh.png"),
        };
        btnRestart.Click += (_, _) => _ = StartShellAsync();

        toolbar.Items.Add(lblShell);
        toolbar.Items.Add(cmbShell);
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(btnRestart);

        // ── Output RichTextBox ────────────────────────────────────────────────
        output = new RichTextBox
        {
            Dock        = DockStyle.Fill,
            Font        = new Font("Consolas", 9.5F),
            BackColor   = Color.FromArgb(12, 12, 12),
            ForeColor   = Color.FromArgb(204, 204, 204),
            ReadOnly    = true,
            ScrollBars  = RichTextBoxScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            WordWrap    = false,
            Visible     = false,
        };

        // ── Input panel ───────────────────────────────────────────────────────
        inputPanel = new Panel { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(4, 2, 4, 2) };
        inputBox = new TextBox
        {
            Dock      = DockStyle.Fill,
            Font      = new Font("Consolas", 9.5F),
            Enabled   = false,
        };
        inputBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Return)
            {
                e.SuppressKeyPress = true;
                string cmd = inputBox.Text;
                inputBox.Clear();
                _ = SendCommandAsync(cmd);
            }
        };
        btnSend = new Button
        {
            Text      = "Send",
            Dock      = DockStyle.Right,
            Width     = 56,
            FlatStyle = FlatStyle.Flat,
            Enabled   = false,
        };
        btnSend.Click += (_, _) =>
        {
            string cmd = inputBox.Text;
            inputBox.Clear();
            _ = SendCommandAsync(cmd);
        };
        inputPanel.Controls.Add(inputBox);
        inputPanel.Controls.Add(btnSend);

        // ── Loading overlay ───────────────────────────────────────────────────
        loadingPanel = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.FromArgb(28, 28, 28),
            Visible   = true,
        };
        loadingLabel = new Label
        {
            AutoSize  = false,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Text      = "Starting shell…",
            Font      = new Font("Segoe UI", 11F),
            ForeColor = Color.FromArgb(160, 160, 160),
            BackColor = Color.Transparent,
        };
        loadingPanel.Controls.Add(loadingLabel);

        Controls.Add(loadingPanel);
        Controls.Add(output);
        Controls.Add(inputPanel);
        Controls.Add(toolbar);

        ResumeLayout();

        Load += async (_, _) => await StartShellAsync();
    }

    // ── Shell lifecycle ───────────────────────────────────────────────────────

    private async Task StartShellAsync()
    {
        _running = false;
        SetLoading(true, "Starting shell…");
        SetInput(false);

        var bytes = ModuleLoader.GetModuleBytes(ModuleFile);
        if (bytes is null)
        {
            SetLoading(true, $"Module not found: {ModuleFile}.enc");
            return;
        }

        _ctx = new ModuleContext(_handler, ModuleId);
        await ModuleDelivery.EnsureDeliveredAsync(_handler, ModuleId, bytes);

        string shell   = cmbShell.SelectedIndex == 1 ? "powershell" : "cmd";
        string cwdPart = string.IsNullOrEmpty(_initialDir)
            ? ""
            : $",\"cwd\":\"{EscJson(_initialDir)}\"";

        var result = await _ctx.ExecuteAsync("start", $"{{\"shell\":\"{shell}\"{cwdPart}}}");

        if (result?.StartsWith("error:") == true)
        {
            AppendLine($"[shell error] {result}");
            SetLoading(false);
            return;
        }

        _running = true;
        SetLoading(false);
        SetInput(true);
        inputBox.Focus();
    }

    // ── Send / receive ────────────────────────────────────────────────────────

    private async Task SendCommandAsync(string cmd)
    {
        if (!_running || _ctx is null || string.IsNullOrWhiteSpace(cmd)) return;

        SetInput(false);
        AppendLine($"> {cmd}");

        var result = await _ctx.ExecuteAsync("exec", cmd);
        if (!string.IsNullOrEmpty(result))
            Append(result);

        SetInput(true);
        inputBox.Focus();
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private void AppendLine(string text) => Append(text + "\r\n");

    private void Append(string text)
    {
        if (IsDisposed) return;
        if (output.InvokeRequired) { output.BeginInvoke(new Action(() => Append(text))); return; }
        output.AppendText(text);
        output.ScrollToCaret();
    }

    private void SetLoading(bool loading, string? msg = null)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action(() => SetLoading(loading, msg))); return; }
        if (msg is not null) loadingLabel.Text = msg;
        loadingPanel.Visible = loading;
        output.Visible       = !loading;
    }

    private void SetInput(bool enabled)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action(() => SetInput(enabled))); return; }
        inputBox.Enabled = enabled;
        btnSend.Enabled  = enabled;
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        if (_ctx is not null)
            _ = _ctx.ExecuteAsync("stop", "");
    }

    private static string EscJson(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
