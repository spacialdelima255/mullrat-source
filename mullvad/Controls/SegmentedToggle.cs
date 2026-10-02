using System.Drawing.Drawing2D;
using System.Windows.Forms.VisualStyles;
using mullvad.Theme;

namespace mullvad.Controls
{
    public class SegmentedToggle : Control
    {
        private int _selected = 0;
        private int _hot      = -1;
        private readonly string[] _labels;

        public event EventHandler? SelectedChanged;

        public int SelectedIndex
        {
            get => _selected;
            set
            {
                if (value == _selected) return;
                _selected = value;
                Invalidate();
                SelectedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public SegmentedToggle(params string[] labels)
        {
            _labels = labels;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        private int SegmentWidth => Width / _labels.Length;

        private int HitTest(int x)
        {
            int idx = x / SegmentWidth;
            return idx >= 0 && idx < _labels.Length ? idx : -1;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            base.OnHandleDestroyed(e);
        }

        private void OnThemeChanged()
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { BeginInvoke(Invalidate); return; }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int hot = HitTest(e.X);
            if (hot != _hot) { _hot = hot; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hot = -1; Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int idx = HitTest(e.X);
            if (idx >= 0 && idx != _selected)
            {
                _selected = idx;
                Invalidate();
                SelectedChanged?.Invoke(this, EventArgs.Empty);
            }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (ThemeManager.Current == AppTheme.Light)
                PaintLight(e.Graphics);
            else
                PaintDark(e.Graphics);
        }

        private void PaintLight(Graphics g)
        {
            int sw = SegmentWidth;
            for (int i = 0; i < _labels.Length; i++)
            {
                var state = _selected == i ? PushButtonState.Pressed
                          : _hot      == i ? PushButtonState.Hot
                          :                  PushButtonState.Normal;

                var clip = new Rectangle(i * sw, 0, i == _labels.Length - 1 ? Width - i * sw : sw, Height);
                g.SetClip(clip);
                ButtonRenderer.DrawButton(g, new Rectangle(0, 0, Width, Height), state);
                g.ResetClip();

                TextRenderer.DrawText(g, _labels[i], Font, clip, SystemColors.ControlText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            using var pen = new Pen(SystemColors.ButtonShadow);
            for (int i = 1; i < _labels.Length; i++)
                g.DrawLine(pen, i * sw, 2, i * sw, Height - 3);
        }

        private void PaintDark(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int sw     = SegmentWidth;
            int radius = 4;

            Color bgNorm    = Color.FromArgb(55, 55, 55);
            Color bgSel     = Color.FromArgb(0, 122, 204);
            Color bgHot     = Color.FromArgb(68, 68, 68);
            Color fgSel     = Color.White;
            Color fgNorm    = Color.FromArgb(212, 212, 212);
            Color borderCol = Color.FromArgb(80, 80, 80);

            using (var path = RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), radius))
            using (var bgBrush = new SolidBrush(bgNorm))
                g.FillPath(bgBrush, path);

            for (int i = 0; i < _labels.Length; i++)
            {
                bool sel = _selected == i;
                bool hot = _hot == i && !sel;
                if (!sel && !hot) continue;

                var clip = new Rectangle(i * sw, 0, i == _labels.Length - 1 ? Width - i * sw : sw, Height);
                g.SetClip(clip);

                var color = sel ? bgSel : bgHot;
                using (var path = RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), radius))
                using (var b = new SolidBrush(color))
                    g.FillPath(b, path);

                g.ResetClip();
            }

            using (var path = RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), radius))
            using (var pen = new Pen(borderCol))
                g.DrawPath(pen, path);

            using (var pen = new Pen(borderCol))
                for (int i = 1; i < _labels.Length; i++)
                    g.DrawLine(pen, i * sw, 3, i * sw, Height - 4);

            g.SmoothingMode = SmoothingMode.Default;
            for (int i = 0; i < _labels.Length; i++)
            {
                var clip = new Rectangle(i * sw, 0, i == _labels.Length - 1 ? Width - i * sw : sw, Height);
                var fg = _selected == i ? fgSel : fgNorm;
                TextRenderer.DrawText(g, _labels[i], Font, clip, fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X,             r.Y,              d, d, 180, 90);
            path.AddArc(r.Right - d,     r.Y,              d, d, 270, 90);
            path.AddArc(r.Right - d,     r.Bottom - d,     d, d,   0, 90);
            path.AddArc(r.X,             r.Bottom - d,     d, d,  90, 90);
            path.CloseAllFigures();
            return path;
        }
    }
}
