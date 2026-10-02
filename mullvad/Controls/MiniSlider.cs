using System;
using System.Drawing;
using System.Windows.Forms;

namespace mullvad.Controls
{
    /// <summary>
    /// Compact horizontal slider drawn with GDI+. Height ~18px — avoids the
    /// ~45px minimum that TrackBar imposes when hosted in a ToolStripControlHost.
    /// </summary>
    internal sealed class MiniSlider : Control
    {
        private int  _min, _max = 100, _val = 50;
        private bool _dragging;

        public event EventHandler? ValueChanged;

        public int Minimum
        {
            get => _min;
            set { _min = value; if (_val < _min) _val = _min; Invalidate(); }
        }

        public int Maximum
        {
            get => _max;
            set { _max = value; if (_val > _max) _val = _max; Invalidate(); }
        }

        public int Value
        {
            get => _val;
            set
            {
                int v = Math.Max(_min, Math.Min(_max, value));
                if (v == _val) return;
                _val = v;
                ValueChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }

        public MiniSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            Height  = 18;
            Width   = 100;
            Cursor  = Cursors.Hand;
            Margin  = Padding.Empty;
            Padding = Padding.Empty;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            int cy = h / 2;
            int tx = ThumbX();
            const int pad = 6;

            // Track background
            using var trackBrush = new SolidBrush(Color.FromArgb(200, 200, 200));
            g.FillRectangle(trackBrush, pad, cy - 1, w - pad * 2, 2);

            // Filled portion
            using var fillBrush = new SolidBrush(Color.FromArgb(100, 140, 220));
            g.FillRectangle(fillBrush, pad, cy - 1, Math.Max(0, tx - pad), 2);

            // Thumb
            var thumbRect = new Rectangle(tx - 5, cy - 5, 10, 10);
            using var thumbBrush = new SolidBrush(Color.FromArgb(70, 120, 200));
            g.FillRectangle(thumbBrush, thumbRect);
        }

        private int ThumbX()
        {
            const int pad = 6;
            int range = _max - _min;
            if (range <= 0) return pad;
            return pad + (int)((_val - _min) / (double)range * (ClientSize.Width - pad * 2));
        }

        private int XToValue(int x)
        {
            const int pad = 6;
            int range = _max - _min;
            if (range <= 0) return _min;
            double t = (x - pad) / (double)(ClientSize.Width - pad * 2);
            return _min + (int)(Math.Max(0, Math.Min(1, t)) * range);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _dragging = true;
            Value = XToValue(e.X);
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging) Value = XToValue(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            Capture   = false;
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }

        // Prevent default background erasure
        protected override void OnPaintBackground(PaintEventArgs e) { }
    }
}
