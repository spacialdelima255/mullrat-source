using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms;

public sealed class InstalledAppsForm : Form
{
    private const string ModuleFile = "mullvad.Module.InstalledApplications";
    private const string ModuleId   = "mullvad.installedapps";

    private readonly ClientHandler _handler;
    private ModuleContext?         _ctx;

    // ── Controls ──────────────────────────────────────────────────────────────
    private ToolStrip            toolbar        = null!;
    private ToolStripButton      btnRefresh     = null!;
    private ToolStripTextBox     txtSearch      = null!;
    private ListView             lvApps         = null!;
    private StatusStrip          statusStrip    = null!;
    private ToolStripStatusLabel statusLabel    = null!;
    private ToolStripProgressBar statusProgress = null!;
    private Panel                loadingPanel   = null!;
    private Label                loadingLabel   = null!;

    private List<AppRow> _allRows = new();

    private static readonly Dictionary<ClientHandler, InstalledAppsForm> _openForms = new();

    public static InstalledAppsForm CreateOrActivate(ClientHandler handler)
    {
        if (_openForms.TryGetValue(handler, out var existing) && !existing.IsDisposed)
        {
            existing.BringToFront();
            return existing;
        }
        var frm = new InstalledAppsForm(handler);
        frm.FormClosed += (_, _) => _openForms.Remove(handler);
        _openForms[handler] = frm;
        return frm;
    }

    private InstalledAppsForm(ClientHandler handler)
    {
        _handler = handler;
        Build();
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    private void Build()
    {
        SuspendLayout();

        Text          = $"Installed Applications — {_handler.Info.Computer}";
        Size          = new Size(1000, 600);
        MinimumSize   = new Size(700, 400);
        StartPosition = FormStartPosition.CenterScreen;
        Font          = new Font("Segoe UI", 9F);
        KeyPreview    = true;

        var iconBmp = IconLoader.Load("mullvad.ico") as System.Drawing.Bitmap
                   ?? IconLoader.Load("mullvad-image.png") as System.Drawing.Bitmap;
        if (iconBmp is not null) try { Icon = System.Drawing.Icon.FromHandle(iconBmp.GetHicon()); } catch { }
        else Icon = SystemIcons.Application;

        // ── Toolbar ──────────────────────────────────────────────────────────
        toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(2) };

        btnRefresh = new ToolStripButton("Refresh") { ToolTipText = "Reload installed applications (F5)" };
        var img = IconLoader.Load("refresh.png");
        if (img is not null) { btnRefresh.Image = img; btnRefresh.DisplayStyle = ToolStripItemDisplayStyle.Image; }

        btnRefresh.Click += (_, _) => _ = RefreshAsync();

        txtSearch = new ToolStripTextBox { Width = 200, ToolTipText = "Filter by name, publisher or version" };
        txtSearch.TextChanged += (_, _) => ApplyFilter();

        toolbar.Items.AddRange(new ToolStripItem[]
        {
            btnRefresh,
            new ToolStripSeparator(),
            new ToolStripLabel("Search: "),
            txtSearch,
        });

        // ── ListView ─────────────────────────────────────────────────────────
        lvApps = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            GridLines     = false,
            MultiSelect   = false,
            SmallImageList = BuildAppIconList(),
        };
        lvApps.Columns.Add("Name",             280);
        lvApps.Columns.Add("Version",          120);
        lvApps.Columns.Add("Publisher",        200);
        lvApps.Columns.Add("Install Date",      95);
        lvApps.Columns.Add("Install Location", 200);

        lvApps.ColumnClick += (_, e) => SortByColumn(e.Column);

        // ── Status strip ──────────────────────────────────────────────────────
        statusStrip    = new StatusStrip();
        statusLabel    = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        statusProgress = new ToolStripProgressBar { Visible = false, Width = 120 };
        statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, statusProgress });

        // ── Loading overlay ───────────────────────────────────────────────────
        loadingPanel = new Panel { Dock = DockStyle.Fill, Visible = true };
        loadingLabel = new Label
        {
            Text      = "Loading installed applications…",
            AutoSize  = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock      = DockStyle.Fill,
            Font      = new Font("Segoe UI", 12F),
        };
        loadingPanel.Controls.Add(loadingLabel);

        Controls.Add(loadingPanel);
        Controls.Add(lvApps);
        Controls.Add(toolbar);
        Controls.Add(statusStrip);

        ResumeLayout();
    }

    // ── OnLoad ────────────────────────────────────────────────────────────────

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        SetStatus("Delivering module…");
        _handler.Disconnected += OnClientDisconnected;
        try
        {
            var moduleBytes = ModuleLoader.GetModuleBytes(ModuleFile);
            if (moduleBytes is null)
            {
                SetStatus("Module file not found — build the InstalledApplications project first");
                loadingPanel.Visible = false;
                return;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            bool ok = await ModuleDelivery.EnsureDeliveredAsync(_handler, ModuleFile, moduleBytes, null, cts.Token);
            if (!ok) { SetStatus("Module delivery failed"); loadingPanel.Visible = false; return; }

            _ctx = new ModuleContext(_handler, ModuleId);
            _ctx.Disconnected += (_, _) => BeginInvoke(() => OnClientDisconnected(_handler));

            loadingPanel.Visible = false;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            SetStatus("Error: " + ex.Message);
            loadingPanel.Visible = false;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _handler.Disconnected -= OnClientDisconnected;
        _ctx?.Dispose();
        base.OnFormClosed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5) { _ = RefreshAsync(); e.Handled = true; }
        base.OnKeyDown(e);
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
        SetBusy(true, "Fetching installed applications…");

        string? json = null;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            json = await _ctx.ExecuteAsync("list", "", cts.Token);
        }
        catch { }

        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            _allRows.Clear();
            lvApps.BeginUpdate();
            lvApps.Items.Clear();

            if (json != null && !json.StartsWith("{\"error\""))
            {
                _allRows = ParseApps(json);
                PopulateList(_allRows);
            }
            else if (json != null)
            {
                SetStatus("Error: " + json);
            }

            lvApps.EndUpdate();
            SetBusy(false);
            SetStatus($"{lvApps.Items.Count} application(s)");
        });
    }

    private void PopulateList(IEnumerable<AppRow> rows)
    {
        lvApps.Items.Clear();
        string filter = txtSearch.Text.Trim();
        foreach (var a in rows)
        {
            if (!string.IsNullOrEmpty(filter) &&
                a.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                a.Publisher.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                a.Version.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var lvi = new ListViewItem(a.Name) { ImageKey = GetAppImageKey(a.Name) };
            lvi.SubItems.Add(a.Version);
            lvi.SubItems.Add(a.Publisher);
            lvi.SubItems.Add(a.InstallDate);
            lvi.SubItems.Add(a.InstallLocation);
            lvi.Tag = a;
            lvApps.Items.Add(lvi);
        }
        SetStatus($"{lvApps.Items.Count} application(s)" + (string.IsNullOrEmpty(filter) ? "" : " (filtered)"));
    }

    private void ApplyFilter()
    {
        lvApps.BeginUpdate();
        PopulateList(_allRows);
        lvApps.EndUpdate();
    }

    // ── Sort ──────────────────────────────────────────────────────────────────

    private int _sortCol  = 0;
    private bool _sortAsc = true;

    private void SortByColumn(int col)
    {
        if (_sortCol == col) _sortAsc = !_sortAsc;
        else { _sortCol = col; _sortAsc = true; }

        _allRows.Sort((a, b) =>
        {
            string sa = col switch { 1 => a.Version, 2 => a.Publisher, 3 => a.InstallDate, 4 => a.InstallLocation, _ => a.Name };
            string sb2 = col switch { 1 => b.Version, 2 => b.Publisher, 3 => b.InstallDate, 4 => b.InstallLocation, _ => b.Name };
            int cmp = string.Compare(sa, sb2, StringComparison.OrdinalIgnoreCase);
            return _sortAsc ? cmp : -cmp;
        });

        lvApps.BeginUpdate();
        PopulateList(_allRows);
        lvApps.EndUpdate();
    }

    // ── Parse ─────────────────────────────────────────────────────────────────

    private static List<AppRow> ParseApps(string json)
    {
        var list = new List<AppRow>();
        try
        {
            var doc = JsonDocument.Parse(json);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                list.Add(new AppRow
                {
                    Name            = GetStr(el, "name"),
                    Version         = GetStr(el, "version"),
                    Publisher       = GetStr(el, "publisher"),
                    InstallDate     = GetStr(el, "install_date"),
                    InstallLocation = GetStr(el, "install_location"),
                });
            }
        }
        catch { }
        return list;
    }

    private static string GetStr(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";

    // ── Status ────────────────────────────────────────────────────────────────

    private void SetBusy(bool busy, string? msg = null)
    {
        btnRefresh.Enabled        = !busy;
        statusProgress.Visible    = busy;
        if (msg is not null) SetStatus(msg);
    }

    private void SetStatus(string msg)
    {
        if (!IsDisposed) BeginInvoke(() => { if (!IsDisposed) statusLabel.Text = msg; });
    }

    // ── App icon helpers ──────────────────────────────────────────────────────

    private static ImageList BuildAppIconList()
    {
        var il = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
        foreach (var key in new[] { "discord", "telegram", "steam", "skype", "wechat",
                                    "application", "exe" })
        {
            var img = IconLoader.Load(key + ".png");
            if (img is not null) il.Images.Add(key, img);
        }
        return il;
    }

    private static string GetAppImageKey(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("discord"))                    return "discord";
        if (n.Contains("telegram"))                   return "telegram";
        if (n.Contains("steam"))                      return "steam";
        if (n.Contains("skype"))                      return "skype";
        if (n.Contains("wechat") || n.Contains("we chat")) return "wechat";
        return "application";
    }

    // ── Data ──────────────────────────────────────────────────────────────────

    private sealed class AppRow
    {
        public string Name = "", Version = "", Publisher = "", InstallDate = "", InstallLocation = "";
    }
}
