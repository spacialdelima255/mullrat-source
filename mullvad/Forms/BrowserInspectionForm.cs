using System.Text.Json;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms;

public sealed class BrowserInspectionForm : Form
{
    private const string ModuleFile = "mullvad.Module.BrowserInspection";
    private const string ModuleId   = "mullvad.browserinspect";

    private readonly ClientHandler _handler;
    private ModuleContext?         _ctx;

    // ── Controls ─────────────────────────────────────────────────────────────
    private ToolStrip            toolbar      = null!;
    private ToolStripButton      btnRefresh   = null!;
    private TreeView             tree         = null!;
    private ContextMenuStrip     nodeMenu     = null!;
    private ToolStripMenuItem    menuExplorer = null!;
    private StatusStrip          statusStrip  = null!;
    private ToolStripStatusLabel statusLabel  = null!;
    private Panel                loadingPanel = null!;
    private Label                loadingLabel = null!;
    private ImageList            imgList      = null!;

    // ── Singleton per handler ─────────────────────────────────────────────────
    private static readonly Dictionary<ClientHandler, BrowserInspectionForm> _active = new();
    public static BrowserInspectionForm CreateOrActivate(ClientHandler h)
    {
        if (_active.TryGetValue(h, out var ex) && !ex.IsDisposed) { ex.BringToFront(); return ex; }
        var frm = new BrowserInspectionForm(h);
        _active[h] = frm;
        frm.FormClosed += (_, _) => _active.Remove(h);
        return frm;
    }

    private BrowserInspectionForm(ClientHandler handler)
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

        Text          = $"Browser Inspection  {_handler.Info.Computer}";
        ClientSize    = new Size(680, 520);
        MinimumSize   = new Size(520, 360);
        Font          = new Font("Segoe UI", 9F);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = true;

        var ico = IconLoader.Load("website.png");
        if (ico != null) Icon = Icon.FromHandle(((Bitmap)ico).GetHicon());

        // ── ImageList ────────────────────────────────────────────────────────
        imgList = new ImageList { ImageSize = new Size(20, 20), ColorDepth = ColorDepth.Depth32Bit };
        imgList.Images.Add("browser", IconLoader.Load("world_go.png") ?? SystemIcons.Application.ToBitmap());
        imgList.Images.Add("profile", IconLoader.Load("user.png")     ?? SystemIcons.Shield.ToBitmap());

        // ── Toolbar ──────────────────────────────────────────────────────────
        toolbar    = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        btnRefresh = new ToolStripButton
        {
            ToolTipText  = "Refresh",
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            Image        = IconLoader.Load("refresh.png"),
        };
        btnRefresh.Click += (_, _) => _ = CollectAsync();
        toolbar.Items.Add(btnRefresh);

        // ── Context menu ─────────────────────────────────────────────────────
        nodeMenu     = new ContextMenuStrip();
        menuExplorer = new ToolStripMenuItem("Follow to File Explorer");
        IconLoader.SetIcon(menuExplorer, "folder.png");
        menuExplorer.Click += MenuExplorer_Click;
        nodeMenu.Items.Add(menuExplorer);

        // ── TreeView ─────────────────────────────────────────────────────────
        tree = new TreeView
        {
            Dock            = DockStyle.Fill,
            ImageList       = imgList,
            ShowNodeToolTips = true,
            Font            = new Font("Segoe UI", 9.5F),
        };
        tree.MouseUp += Tree_MouseUp;

        // ── Status strip ─────────────────────────────────────────────────────
        statusStrip = new StatusStrip();
        statusLabel = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        statusStrip.Items.Add(statusLabel);

        // ── Loading overlay ───────────────────────────────────────────────────
        loadingPanel = new Panel { Dock = DockStyle.Fill, Visible = true };
        loadingLabel = new Label
        {
            Text      = "Delivering module to client…",
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font      = new Font("Segoe UI", 10F),
        };
        loadingPanel.Controls.Add(loadingLabel);

        Controls.Add(tree);
        Controls.Add(loadingPanel);
        Controls.Add(statusStrip);
        Controls.Add(toolbar);
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
        btnRefresh.Enabled = false;
        _handler.Disconnected += OnDisconnected;

        var progress = new Progress<string>(msg =>
        {
            loadingLabel.Text = msg;
            statusLabel.Text  = msg;
        });

        var bytes = ModuleLoader.GetModuleBytes(ModuleFile);
        if (bytes == null)
        {
            loadingLabel.Text = "Module file not found. Build the BrowserInspection project first.";
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        bool ok = await ModuleDelivery.EnsureDeliveredAsync(_handler, ModuleFile, bytes, progress, cts.Token);
        if (!ok)
        {
            loadingLabel.Text = "Failed to deliver module.";
            return;
        }

        _ctx = new ModuleContext(_handler, ModuleId);
        _ctx.Disconnected += (_, _) => BeginInvoke(() => OnDisconnected(_handler));
        loadingPanel.Visible = false;
        btnRefresh.Enabled   = true;

        await CollectAsync();
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        _handler.Disconnected -= OnDisconnected;
        _ctx?.Dispose();
    }

    private void OnDisconnected(ClientHandler _)
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            btnRefresh.Enabled   = false;
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
        btnRefresh.Enabled = false;
        statusLabel.Text   = "Inspecting browsers…";
        try
        {
            var json = await _ctx.ExecuteAsync("inspect", "");
            BuildTree(json);
        }
        catch (OperationCanceledException) { statusLabel.Text = "Timed out."; }
        catch (Exception ex)               { statusLabel.Text = $"Error: {ex.Message}"; }
        finally { btnRefresh.Enabled = true; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Tree building
    // ─────────────────────────────────────────────────────────────────────────
    private void BuildTree(string json)
    {
        tree.BeginUpdate();
        tree.Nodes.Clear();

        // Clean up images beyond the two defaults
        while (imgList.Images.Count > 2)
            imgList.Images.RemoveAt(imgList.Images.Count - 1);

        try
        {
            using var doc      = JsonDocument.Parse(json);
            var       root     = doc.RootElement;

            if (root.TryGetProperty("error", out var err))
            {
                statusLabel.Text = $"Module error: {err.GetString()}";
                tree.EndUpdate();
                return;
            }

            if (!root.TryGetProperty("browsers", out var browsers))
            {
                statusLabel.Text = "No browser data received.";
                tree.EndUpdate();
                return;
            }

            int browserCount = 0;
            int profileCount = 0;

            foreach (var b in browsers.EnumerateArray())
            {
                string name     = Str(b, "name");
                string version  = Str(b, "version");
                string exe      = Str(b, "exe");
                string dataPath = Str(b, "data_path");
                string iconB64  = Str(b, "icon_b64");

                // Add browser icon to ImageList
                string imgKey = "browser_" + browserCount;
                if (!string.IsNullOrEmpty(iconB64))
                {
                    try
                    {
                        byte[] bytes = Convert.FromBase64String(iconB64);
                        using var ms = new MemoryStream(bytes);
                        var bmp = new Bitmap(ms);
                        imgList.Images.Add(imgKey, bmp);
                    }
                    catch { imgKey = "browser"; }
                }
                else
                {
                    imgKey = "browser";
                }

                // Browser node
                string header = string.IsNullOrEmpty(version) ? name : $"{name}  —  v{version}";
                var bNode = new TreeNode(header)
                {
                    ImageKey         = imgKey,
                    SelectedImageKey = imgKey,
                    ToolTipText      = exe,
                    Tag              = new NodeData { Path = dataPath, IsProfile = false },
                };

                // Detail sub-nodes (not right-clickable profile paths)
                bNode.Nodes.Add(MakeInfoNode($"Executable:   {exe}"));
                bNode.Nodes.Add(MakeInfoNode($"Data path:    {dataPath}"));

                // Profile nodes
                if (b.TryGetProperty("profiles", out var profiles))
                {
                    foreach (var p in profiles.EnumerateArray())
                    {
                        string display    = Str(p, "display");
                        string email      = Str(p, "email");
                        string profilePath = Str(p, "path");
                        string folder     = Str(p, "folder");

                        string label = display;
                        if (!string.IsNullOrEmpty(email)) label += $"  ({email})";

                        var pNode = new TreeNode(label)
                        {
                            ImageKey         = "profile",
                            SelectedImageKey = "profile",
                            ToolTipText      = profilePath,
                            Tag              = new NodeData { Path = profilePath, IsProfile = true },
                        };
                        pNode.Nodes.Add(MakeInfoNode($"Folder:  {folder}"));
                        pNode.Nodes.Add(MakeInfoNode($"Path:    {profilePath}"));

                        bNode.Nodes.Add(pNode);
                        profileCount++;
                    }
                }

                tree.Nodes.Add(bNode);
                bNode.Expand();
                browserCount++;
            }

            statusLabel.Text = $"Found {browserCount} browser(s)  ·  {profileCount} profile(s)  ·  refreshed at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Parse error: {ex.Message}";
        }

        tree.EndUpdate();
    }

    private static TreeNode MakeInfoNode(string text) =>
        new TreeNode(text) { ForeColor = SystemColors.GrayText, Tag = new NodeData { IsProfile = false } };

    // ─────────────────────────────────────────────────────────────────────────
    //  Right-click → open folder on client
    // ─────────────────────────────────────────────────────────────────────────
    private void Tree_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        var hit = tree.HitTest(e.Location);
        if (hit.Node == null) return;
        tree.SelectedNode = hit.Node;

        if (hit.Node.Tag is NodeData nd && nd.IsProfile && !string.IsNullOrEmpty(nd.Path))
        {
            menuExplorer.Tag = nd.Path;
            menuExplorer.Enabled = true;
            nodeMenu.Show(tree, e.Location);
        }
    }

    private void MenuExplorer_Click(object? sender, EventArgs e)
    {
        if (_ctx == null) return;
        string path = (menuExplorer.Tag as string) ?? "";
        if (string.IsNullOrEmpty(path)) return;

        _ = Task.Run(async () =>
        {
            try
            {
                string payload = "{\"path\":\"" + path.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
                await _ctx.ExecuteAsync("open_folder", payload);
            }
            catch { }
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private static string Str(JsonElement e, string key)
        => e.TryGetProperty(key, out var v) ? (v.GetString() ?? "") : "";

    private class NodeData
    {
        public string Path      = "";
        public bool   IsProfile;
    }
}
