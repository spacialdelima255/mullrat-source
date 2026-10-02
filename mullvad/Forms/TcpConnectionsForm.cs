using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;
using System.Text.Json;

namespace mullvad.Forms
{
    public sealed class TcpConnectionsForm : Form
    {
        private const string ModuleFile = "mullvad.Module.TcpConnections";
        private const string ModuleId   = "mullvad.tcpconnections";

        private readonly ClientHandler _handler;
        private ModuleContext?         _ctx;

        private readonly ListView          _lv;
        private readonly ToolStripMenuItem _menuRefresh;
        private readonly ToolStripMenuItem _menuClose;
        private readonly StatusStrip       _status;
        private readonly ToolStripStatusLabel _statusLabel;

        private readonly Dictionary<string, ListViewGroup> _groups = new();

        private static readonly Dictionary<ClientHandler, TcpConnectionsForm> _openForms = new();

        public static TcpConnectionsForm CreateOrActivate(ClientHandler handler)
        {
            if (_openForms.TryGetValue(handler, out var existing) && !existing.IsDisposed)
            {
                existing.BringToFront();
                return existing;
            }
            var frm = new TcpConnectionsForm(handler);
            frm.FormClosed += (_, _) => _openForms.Remove(handler);
            _openForms[handler] = frm;
            return frm;
        }

        private TcpConnectionsForm(ClientHandler handler)
        {
            _handler = handler;

            Text            = $"TCP Connections — {handler.Info.Computer}";
            Size            = new System.Drawing.Size(740, 460);
            MinimumSize     = new System.Drawing.Size(540, 300);
            Font            = new System.Drawing.Font("Segoe UI", 8.25f);
            StartPosition   = FormStartPosition.CenterScreen;

            TrySetIcon("transmit_blue.png");

            // ── status strip ─────────────────────────────────────────
            _status      = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("Loading...") { Spring = true, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            _status.Items.Add(_statusLabel);

            // ── context menu ─────────────────────────────────────────
            _menuRefresh = new ToolStripMenuItem("Refresh");
            _menuClose   = new ToolStripMenuItem("Close Connection");
            TryMenuIcon(_menuRefresh, "refresh.png");
            TryMenuIcon(_menuClose,   "server_disconnect.png");
            _menuRefresh.Click += (_, _) => _ = CollectAsync();
            _menuClose.Click   += MenuClose_Click;

            var ctx = new ContextMenuStrip();
            ctx.Items.Add(_menuRefresh);
            ctx.Items.Add(_menuClose);
            ctx.Opening += (_, _) => _menuClose.Enabled = _lv.SelectedItems.Count > 0;

            // ── listview ─────────────────────────────────────────────
            _lv = new ListView
            {
                Dock             = DockStyle.Fill,
                View             = View.Details,
                FullRowSelect    = true,
                GridLines        = false,
                UseCompatibleStateImageBehavior = false,
                ContextMenuStrip = ctx,
            };
            _lv.Columns.Add("Process",        180);
            _lv.Columns.Add("Local Address",  100);
            _lv.Columns.Add("Local Port",      80);
            _lv.Columns.Add("Remote Address", 100);
            _lv.Columns.Add("Remote Port",     80);
            _lv.Columns.Add("State",          100);
            _lv.ColumnClick += (_, e) => { /* sorting placeholder */ };

            Controls.Add(_lv);
            Controls.Add(_status);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _ = InitAsync();
        }

        private async Task InitAsync()
        {
            _statusLabel.Text = "Delivering module…";
            try
            {
                var bytes = ModuleLoader.GetModuleBytes(ModuleFile);
                if (bytes is null) { _statusLabel.Text = "Module not found."; return; }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                bool ok = await ModuleDelivery.EnsureDeliveredAsync(_handler, ModuleFile, bytes, null, cts.Token);
                if (!ok) { _statusLabel.Text = "Module delivery failed."; return; }

                _ctx = new ModuleContext(_handler, ModuleId);
                _ctx.Disconnected += (_, _) => BeginInvoke(() => _statusLabel.Text = "Client disconnected.");
                await CollectAsync();
            }
            catch (Exception ex)
            {
                if (!IsDisposed) BeginInvoke(() => _statusLabel.Text = $"Error: {ex.Message}");
            }
        }

        private async Task CollectAsync()
        {
            if (_ctx is null) return;
            BeginInvoke(() => _statusLabel.Text = "Collecting…");
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var json = await _ctx.ExecuteAsync("collect", "", cts.Token);
                if (!IsDisposed) BeginInvoke(() => ParseAndDisplay(json));
            }
            catch (Exception ex)
            {
                if (!IsDisposed) BeginInvoke(() => _statusLabel.Text = $"Error: {ex.Message}");
            }
        }

        private void ParseAndDisplay(string json)
        {
            _lv.BeginUpdate();
            _lv.Items.Clear();
            _lv.Groups.Clear();
            _groups.Clear();

            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    string process = el.TryGetProperty("process",        out var p) ? p.GetString() ?? "" : "";
                    string localA  = el.TryGetProperty("local_address",  out var la) ? la.GetString() ?? "" : "";
                    string localP  = el.TryGetProperty("local_port",     out var lp) ? lp.GetRawText() : "";
                    string remA    = el.TryGetProperty("remote_address", out var ra) ? ra.GetString() ?? "" : "";
                    string remP    = el.TryGetProperty("remote_port",    out var rp) ? rp.GetRawText() : "";
                    string state   = el.TryGetProperty("state",          out var s) ? s.GetString() ?? "" : "";

                    if (!_groups.TryGetValue(state, out var grp))
                    {
                        grp = new ListViewGroup(state, state);
                        _lv.Groups.Add(grp);
                        _groups[state] = grp;
                    }

                    var lvi = new ListViewItem(new[] { process, localA, localP, remA, remP, state }, grp);
                    _lv.Items.Add(lvi);
                }
            }
            catch { }

            _lv.EndUpdate();
            _statusLabel.Text = $"{_lv.Items.Count} connection(s) — {DateTime.Now:HH:mm:ss}";
        }

        private void MenuClose_Click(object? sender, EventArgs e)
        {
            if (_ctx is null || _lv.SelectedItems.Count == 0) return;
            var sel = _lv.SelectedItems[0];
            var payload = $"{{\"local_address\":\"{Esc(sel.SubItems[1].Text)}\",\"local_port\":{sel.SubItems[2].Text},\"remote_address\":\"{Esc(sel.SubItems[3].Text)}\",\"remote_port\":{sel.SubItems[4].Text}}}";
            _ = Task.Run(async () =>
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    await _ctx.ExecuteAsync("close", payload, cts.Token);
                }
                catch { }
                if (!IsDisposed) BeginInvoke(CollectAsync);
            });
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _ctx?.Dispose();
            base.OnFormClosed(e);
        }

        private void TrySetIcon(string name)
        {
            var bmp = IconLoader.Load(name) as System.Drawing.Bitmap;
            if (bmp is not null)
                try { Icon = System.Drawing.Icon.FromHandle(bmp.GetHicon()); } catch { }
        }

        private static void TryMenuIcon(ToolStripMenuItem item, string name)
        {
            item.Image = IconLoader.Load(name);
        }

        private static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
