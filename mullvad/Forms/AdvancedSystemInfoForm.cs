using System.Text.Json;
using mullvad.Database;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms;

public sealed class AdvancedSystemInfoForm : Form
{
    private const string ModuleFile = "mullvad.Module.AdvancedSystemInformation";
    private const string ModuleId   = "mullvad.advsysinfo";

    private readonly ClientHandler _handler;
    private ModuleContext?         _ctx;

    // ── Controls ──────────────────────────────────────────────────────────────
    private ToolStrip            toolbar        = null!;
    private ToolStripButton      btnRefresh     = null!;
    private ToolStripButton      btnSaveDb      = null!;
    private ToolStripLabel       lblFind        = null!;
    private ToolStripTextBox     txtFind        = null!;
    private ToolStripButton      btnFind        = null!;
    private TreeView             treeView       = null!;
    private ListView             listView       = null!;
    private StatusStrip          statusStrip    = null!;
    private ToolStripStatusLabel statusLabel    = null!;
    private ToolStripProgressBar statusProgress = null!;
    private Panel                loadingPanel   = null!;
    private Label                loadingLabel   = null!;

    // Collected data
    private List<(string category, string item, string value)> _data = new();

    //── Category → tree path mapping ─────────────────────────────────────────
    // Key = category string from module JSON, Value = tree node key
    private static readonly Dictionary<string, string> CategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["System Summary"]                        = "System Summary",
        // Hardware Resources
        ["Hardware Resources/Conflicts/Sharing"]  = "Conflicts/Sharing",
        ["Hardware Resources/DMA"]                = "DMA",
        ["Hardware Resources/Forced Hardware"]    = "Forced Hardware",
        ["Hardware Resources/I/O"]                = "I/O",
        ["Hardware Resources/IRQs"]               = "IRQs",
        ["Hardware Resources/Memory"]             = "Memory",
        // Components
        ["Components/Display"]                    = "Display",
        ["Components/Sound Device"]               = "Sound Device",
        ["Components/Network"]                    = "Network",
        ["Components/Ports"]                      = "Ports",
        ["Components/Storage"]                    = "Storage",
        ["Components/USB"]                        = "USB",
        // Software Environment
        ["Software Environment/System Drivers"]   = "System Drivers",
        ["Software Environment/Running Tasks"]    = "Running Tasks",
        ["Software Environment/Loaded Modules"]   = "Loaded Modules",
        ["Software Environment/Services"]         = "Services",
        ["Software Environment/Program Groups"]   = "Program Groups",
        ["Software Environment/Startup Programs"] = "Startup Programs",
        ["Software Environment/OLE Registration"] = "OLE Registration",
        ["Software Environment/Windows Error Reporting"] = "Windows Error Reporting",
    };

    public AdvancedSystemInfoForm(ClientHandler handler)
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

        Text          = $"Advanced System Information  —  {_handler.Info.Computer}";
        ClientSize    = new Size(900, 600);
        Font          = new Font("Segoe UI", 9F);
        MinimumSize   = new Size(640, 400);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = true;

        // ── Toolbar ──────────────────────────────────────────────────────────
        toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };

        btnRefresh = MakeBtn("⟳", "Refresh", "refresh.png");
        btnRefresh.Click += (_, _) => _ = CollectAsync();

        btnSaveDb = MakeBtn("💾", "Save to Database", "information.png");
        btnSaveDb.Click += (_, _) => SaveToDb();

        lblFind = new ToolStripLabel("Find: ");
        txtFind = new ToolStripTextBox { Width = 140, BorderStyle = BorderStyle.FixedSingle };
        txtFind.KeyDown += (_, e) => { if (e.KeyCode == Keys.Return) DoFind(); };

        btnFind = MakeBtn("🔍", "Find", null);
        btnFind.Text         = "Find";
        btnFind.DisplayStyle = ToolStripItemDisplayStyle.Text;
        btnFind.Click += (_, _) => DoFind();

        toolbar.Items.AddRange(new ToolStripItem[]
        {
            btnRefresh, new ToolStripSeparator(), btnSaveDb,
            new ToolStripSeparator(), lblFind, txtFind, btnFind,
        });

        // ── TreeView (left panel) ─────────────────────────────────────────────
        treeView = new TreeView
        {
            Dock          = DockStyle.Fill,
            HideSelection = false,
            Font          = new Font("Segoe UI", 9F),
        };
        BuildTree();
        treeView.AfterSelect += OnTreeNodeSelected;

        var leftPanel = new Panel { Dock = DockStyle.Left, Width = 220 };
        leftPanel.Controls.Add(treeView);

        var splitter = new Splitter { Dock = DockStyle.Left, Width = 4 };

        // ── ListView (right panel) ────────────────────────────────────────────
        listView = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = false,
            Font          = new Font("Segoe UI", 9F),
        };
        listView.Columns.Add("Item",  250);
        listView.Columns.Add("Value", -2);

        var rightPanel = new Panel { Dock = DockStyle.Fill };
        rightPanel.Controls.Add(listView);

        // ── Status strip ─────────────────────────────────────────────────────
        statusStrip    = new StatusStrip();
        statusLabel    = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        statusProgress = new ToolStripProgressBar { Visible = false, Width = 120 };
        statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, statusProgress });

        // ── Loading overlay ───────────────────────────────────────────────────
        loadingPanel = new Panel
        {
            Dock = DockStyle.Fill, BackColor = Color.FromArgb(200, SystemColors.Control),
            Visible = true,
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
        Controls.Add(rightPanel);
        Controls.Add(splitter);
        Controls.Add(leftPanel);
        Controls.Add(toolbar);
        Controls.Add(statusStrip);

        loadingPanel.BringToFront();

        ResumeLayout(false);
        PerformLayout();

        Load       += OnLoad;
        FormClosed += OnFormClosed;
    }

    private void BuildTree()
    {
        treeView.BeginUpdate();
        treeView.Nodes.Clear();

        var root = new TreeNode("System Summary") { Name = "System Summary" };

        var hw = new TreeNode("Hardware Resources") { Name = "Hardware Resources" };
        hw.Nodes.AddRange(new[]
        {
            new TreeNode("Conflicts/Sharing") { Name = "Conflicts/Sharing" },
            new TreeNode("DMA")               { Name = "DMA" },
            new TreeNode("Forced Hardware")   { Name = "Forced Hardware" },
            new TreeNode("I/O")               { Name = "I/O" },
            new TreeNode("IRQs")              { Name = "IRQs" },
            new TreeNode("Memory")            { Name = "Memory" },
        });

        var comp = new TreeNode("Components") { Name = "Components" };
        comp.Nodes.AddRange(new[]
        {
            new TreeNode("Display")      { Name = "Display" },
            new TreeNode("Sound Device") { Name = "Sound Device" },
            new TreeNode("Network")      { Name = "Network" },
            new TreeNode("Ports")        { Name = "Ports" },
            new TreeNode("Storage")      { Name = "Storage" },
            new TreeNode("USB")          { Name = "USB" },
        });

        var sw = new TreeNode("Software Environment") { Name = "Software Environment" };
        sw.Nodes.AddRange(new[]
        {
            new TreeNode("System Drivers")         { Name = "System Drivers" },
            new TreeNode("Running Tasks")          { Name = "Running Tasks" },
            new TreeNode("Loaded Modules")         { Name = "Loaded Modules" },
            new TreeNode("Services")               { Name = "Services" },
            new TreeNode("Program Groups")         { Name = "Program Groups" },
            new TreeNode("Startup Programs")       { Name = "Startup Programs" },
            new TreeNode("OLE Registration")       { Name = "OLE Registration" },
            new TreeNode("Windows Error Reporting") { Name = "Windows Error Reporting" },
        });

        treeView.Nodes.Add(root);
        treeView.Nodes.Add(hw);
        treeView.Nodes.Add(comp);
        treeView.Nodes.Add(sw);

        treeView.ExpandAll();
        treeView.EndUpdate();
    }

    private static ToolStripButton MakeBtn(string text, string tip, string? iconFile)
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

    // ─────────────────────────────────────────────────────────────────────────
    //  Load — deliver module then collect
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
            loadingLabel.Text = "Module file not found.\nBuild the AdvancedSystemInformation project first.";
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

        await CollectAsync();
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        _handler.Disconnected -= OnClientDisconnected;
        _ctx?.Dispose();
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
    //  Collect
    // ─────────────────────────────────────────────────────────────────────────
    private async Task CollectAsync()
    {
        if (_ctx == null) return;
        SetBusy(true);
        statusLabel.Text = "Collecting advanced system information…";
        try
        {
            var json = await _ctx.ExecuteAsync("collect", "");
            ParseData(json);
            // Re-display whatever node is selected
            if (treeView.SelectedNode != null)
                ShowNodeData(treeView.SelectedNode.Name);
            else
                // Select System Summary by default
                SelectNode("System Summary");
        }
        catch (OperationCanceledException) { statusLabel.Text = "Timed out."; }
        catch (Exception ex)               { statusLabel.Text = $"Error: {ex.Message}"; }
        finally { SetBusy(false); }
    }

    private void ParseData(string json)
    {
        _data.Clear();
        try
        {
            var doc = JsonDocument.Parse(json).RootElement;
            if (doc.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.EnumerateArray())
                {
                    _data.Add((Str(el, "category"), Str(el, "item"), Str(el, "value")));
                }
            }
            statusLabel.Text = $"Collected {_data.Count} items at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex) { statusLabel.Text = $"Parse error: {ex.Message}"; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Tree selection
    // ─────────────────────────────────────────────────────────────────────────
    private void OnTreeNodeSelected(object? sender, TreeViewEventArgs e)
    {
        if (e.Node == null) return;
        ShowNodeData(e.Node.Name);
    }

    private void ShowNodeData(string nodeName)
    {
        listView.BeginUpdate();
        listView.Items.Clear();

        IEnumerable<(string category, string item, string value)> rows;

        if (nodeName == "System Summary")
        {
            // Show rows where category is "System Summary" (exact) or uncategorised
            rows = _data.Where(d =>
                d.category.Equals("System Summary", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(d.category));
        }
        else if (nodeName is "Hardware Resources" or "Components" or "Software Environment")
        {
            // Parent node — show all children
            rows = _data.Where(d =>
                d.category.StartsWith(nodeName + "/", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            // Leaf node — match "ParentGroup/NodeName" or just "NodeName"
            rows = _data.Where(d =>
                d.category.EndsWith("/" + nodeName, StringComparison.OrdinalIgnoreCase) ||
                d.category.Equals(nodeName, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var (_, item, value) in rows)
        {
            var lvi = new ListViewItem(item);
            lvi.SubItems.Add(value);
            listView.Items.Add(lvi);
        }

        listView.EndUpdate();
        statusLabel.Text = $"Showing {listView.Items.Count} items for '{nodeName}'";
    }

    private void SelectNode(string name)
    {
        TreeNode? Find(TreeNodeCollection nodes)
        {
            foreach (TreeNode n in nodes)
            {
                if (n.Name == name) return n;
                var found = Find(n.Nodes);
                if (found != null) return found;
            }
            return null;
        }

        var node = Find(treeView.Nodes);
        if (node != null) treeView.SelectedNode = node;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Find
    // ─────────────────────────────────────────────────────────────────────────
    private void DoFind()
    {
        var q = txtFind.Text.Trim();
        if (string.IsNullOrEmpty(q)) return;

        listView.BeginUpdate();
        listView.Items.Clear();

        foreach (var (cat, item, value) in _data)
        {
            if (item.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                value.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                cat.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                var lvi = new ListViewItem(item);
                lvi.SubItems.Add(value);
                listView.Items.Add(lvi);
            }
        }

        listView.EndUpdate();
        statusLabel.Text = $"Found {listView.Items.Count} result(s) for '{q}'";
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Save to DB
    // ─────────────────────────────────────────────────────────────────────────
    private void SaveToDb()
    {
        if (_data.Count == 0) { statusLabel.Text = "Nothing to save."; return; }
        ServerDatabase.SaveAdvancedInfo(_handler.Info.Id, _data);
        ServerDatabase.UpsertClient(_handler.Info);
        statusLabel.Text = $"Saved {_data.Count} rows to database.";
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private void SetToolbarEnabled(bool on)
    {
        foreach (ToolStripItem i in toolbar.Items) i.Enabled = on;
    }

    private void SetBusy(bool busy)
    {
        statusProgress.Visible = busy;
        if (busy) statusProgress.Style = ProgressBarStyle.Marquee;
    }

    private static string Str(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";
}

