using System.Runtime.InteropServices;
using System.Text.Json;

namespace mullvad.Theme
{
    public enum AppTheme { Light, Dark }

    public static class ThemeManager
    {
        public static AppTheme Current  { get; private set; } = AppTheme.Light;
        public static bool     ShowMole { get; private set; } = true;
        public static event Action? ThemeChanged;
        public static event Action? MoleToggleChanged;

        private static readonly string _settingsFile = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        // ── Palette ─────────────────────────────────────────────────────────
        private static readonly Color DarkFormBg    = Color.FromArgb(28,  28,  28);
        private static readonly Color DarkPanelBg   = Color.FromArgb(37,  37,  38);
        private static readonly Color DarkInputBg   = Color.FromArgb(30,  30,  30);
        private static readonly Color DarkFgColor   = Color.FromArgb(212, 212, 212);
        private static readonly Color DarkBorder    = Color.FromArgb(60,  60,  60);
        private static readonly Color DarkBtnBg     = Color.FromArgb(55,  55,  55);
        private static readonly Color DarkMenuBg    = Color.FromArgb(45,  45,  48);
        private static readonly Color DarkStripBg   = Color.FromArgb(37,  37,  38);
        private static readonly Color DarkHdrBg     = Color.FromArgb(45,  45,  48);
        private static readonly Color DarkHdrLine   = Color.FromArgb(62,  62,  64);

        public static Color BackColor    => Current == AppTheme.Dark ? DarkFormBg  : SystemColors.Control;
        public static Color PanelColor   => Current == AppTheme.Dark ? DarkPanelBg : SystemColors.Control;
        public static Color ControlColor => Current == AppTheme.Dark ? DarkInputBg : SystemColors.Window;
        public static Color ForeColor    => Current == AppTheme.Dark ? DarkFgColor : SystemColors.ControlText;
        public static Color MenuBg       => Current == AppTheme.Dark ? DarkMenuBg  : SystemColors.Menu;
        public static Color MenuFg       => Current == AppTheme.Dark ? DarkFgColor : SystemColors.MenuText;
        public static Color StripBg      => Current == AppTheme.Dark ? DarkStripBg : SystemColors.Control;

        // ── Win32 P/Invoke ───────────────────────────────────────────────────
        [DllImport("dwmapi.dll", ExactSpelling = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint attr, ref int value, uint size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern int SetWindowTheme(IntPtr hwnd, string? sub, string? idList);

        private const uint DWMWA_DARK_MODE = 20;

        private static void TitleBarDark(IntPtr hwnd, bool dark)
        {
            try { int v = dark ? 1 : 0; DwmSetWindowAttribute(hwnd, DWMWA_DARK_MODE, ref v, 4); }
            catch { }
        }

        private static void NativeControlTheme(IntPtr hwnd, bool dark)
        {
            try { SetWindowTheme(hwnd, dark ? "DarkMode_Explorer" : null, null); }
            catch { }
        }

        // ── Tracking for converted ListViews ─────────────────────────────────
        private static readonly HashSet<ListView> _managedListViews = new();

        // ── Persistence ─────────────────────────────────────────────────────
        public static void Load()
        {
            try
            {
                if (!File.Exists(_settingsFile)) return;
                var json = File.ReadAllText(_settingsFile);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("theme", out var t) &&
                    Enum.TryParse<AppTheme>(t.GetString(), out var theme))
                    Current = theme;
                if (doc.RootElement.TryGetProperty("showMole", out var m) && m.ValueKind == JsonValueKind.False)
                    ShowMole = false;
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(_settingsFile,
                    $"{{\"theme\":\"{Current}\",\"showMole\":{(ShowMole ? "true" : "false")}}}");
            }
            catch { }
        }

        public static void Set(AppTheme theme)
        {
            if (Current == theme) return;
            Current = theme;
            Save();
            ApplyToAll();
            ThemeChanged?.Invoke();
        }

        public static void SetShowMole(bool show)
        {
            if (ShowMole == show) return;
            ShowMole = show;
            Save();
            MoleToggleChanged?.Invoke();
        }

        // ── Apply to all open forms ──────────────────────────────────────────
        public static void ApplyToAll()
        {
            foreach (Form f in Application.OpenForms.Cast<Form>().ToList())
                ApplyForm(f);
        }

        public static void ApplyForm(Form form)
        {
            if (form is null || form.IsDisposed) return;
            if (form.InvokeRequired) { form.BeginInvoke(() => ApplyForm(form)); return; }

            // Title bar
            if (form.IsHandleCreated)
                TitleBarDark(form.Handle, Current == AppTheme.Dark);
            else
                form.HandleCreated += (_, _) => TitleBarDark(form.Handle, Current == AppTheme.Dark);

            bool dark = Current == AppTheme.Dark;
            form.BackColor = dark ? DarkFormBg : SystemColors.Control;
            form.ForeColor = dark ? DarkFgColor : SystemColors.ControlText;

            form.SuspendLayout();
            try { ApplyDeep(form); }
            finally { form.ResumeLayout(false); form.Invalidate(true); }
        }

        public static void ApplyDeep(Control root)
        {
            foreach (Control c in root.Controls)
                ApplyControlDeep(c);
        }

        public static void ApplyControlDeep(Control ctrl)
        {
            if (ctrl is null) return;
            if (Current == AppTheme.Dark) ApplyDark(ctrl);
            else                          ApplyLight(ctrl);
        }

        // ── Dark ─────────────────────────────────────────────────────────────
        private static void ApplyDark(Control ctrl)
        {
            switch (ctrl)
            {
                case MenuStrip ms:
                    ms.BackColor = DarkMenuBg;
                    ms.ForeColor = DarkFgColor;
                    ms.Renderer  = new DarkStripRenderer();
                    ApplyMenuItems(ms.Items, dark: true);
                    return;

                case StatusStrip ss:
                    ss.BackColor = DarkStripBg;
                    ss.ForeColor = DarkFgColor;
                    ss.Renderer  = new DarkStripRenderer();
                    foreach (ToolStripItem i in ss.Items)
                    { i.BackColor = DarkStripBg; i.ForeColor = DarkFgColor; }
                    return;

                case ToolStrip ts:
                    ts.BackColor = DarkStripBg;
                    ts.ForeColor = DarkFgColor;
                    ts.Renderer  = new DarkStripRenderer();
                    ApplyToolStripItems(ts.Items, dark: true);
                    return;

                case DataGridView dgv:
                    dgv.BackgroundColor = DarkInputBg;
                    dgv.GridColor       = DarkBorder;
                    dgv.DefaultCellStyle.BackColor          = DarkInputBg;
                    dgv.DefaultCellStyle.ForeColor          = DarkFgColor;
                    dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(62, 62, 64);
                    dgv.DefaultCellStyle.SelectionForeColor = DarkFgColor;
                    dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(33, 33, 33);
                    dgv.ColumnHeadersDefaultCellStyle.BackColor          = DarkHdrBg;
                    dgv.ColumnHeadersDefaultCellStyle.ForeColor          = DarkFgColor;
                    dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = DarkHdrBg;
                    return;

                case ListView lv:
                    lv.BackColor = DarkInputBg;
                    lv.ForeColor = DarkFgColor;
                    if (lv.IsHandleCreated) NativeControlTheme(lv.Handle, true);
                    EnsureDarkColumnHeaders(lv);
                    return;

                case TreeView tv:
                    tv.BackColor = DarkInputBg;
                    tv.ForeColor = DarkFgColor;
                    tv.LineColor = DarkBorder;
                    if (tv.IsHandleCreated) NativeControlTheme(tv.Handle, true);
                    return;

                case ListBox lb:
                    lb.BackColor = DarkInputBg;
                    lb.ForeColor = DarkFgColor;
                    return;

                case TextBox tb:
                    tb.BackColor = DarkInputBg;
                    tb.ForeColor = DarkFgColor;
                    tb.BorderStyle = BorderStyle.FixedSingle;
                    if (tb.IsHandleCreated) NativeControlTheme(tb.Handle, true);
                    return;

                case NumericUpDown nud:
                    nud.BackColor = DarkInputBg;
                    nud.ForeColor = DarkFgColor;
                    return;

                case ComboBox cb:
                    cb.BackColor = DarkInputBg;
                    cb.ForeColor = DarkFgColor;
                    return;

                case CheckBox chk:
                    chk.BackColor = Color.Transparent;
                    chk.ForeColor = DarkFgColor;
                    return;

                case RadioButton rb:
                    rb.BackColor = Color.Transparent;
                    rb.ForeColor = DarkFgColor;
                    return;

                case Label lbl:
                    lbl.BackColor = Color.Transparent;
                    lbl.ForeColor = DarkFgColor;
                    return;

                case Button btn:
                    btn.BackColor = DarkBtnBg;
                    btn.ForeColor = DarkFgColor;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = DarkBorder;
                    return;

                case PictureBox pb:
                    pb.BackColor = Color.Transparent;
                    return;

                case SplitContainer sc:
                    sc.BackColor        = DarkBorder;
                    sc.Panel1.BackColor = DarkPanelBg;
                    sc.Panel2.BackColor = DarkPanelBg;
                    foreach (Control c in sc.Panel1.Controls) ApplyDark(c);
                    foreach (Control c in sc.Panel2.Controls) ApplyDark(c);
                    return;

                case GroupBox gb:
                    gb.BackColor = DarkPanelBg;
                    gb.ForeColor = DarkFgColor;
                    foreach (Control c in gb.Controls) ApplyDark(c);
                    return;

                case TabControl tc:
                    tc.BackColor = DarkPanelBg;
                    tc.ForeColor = DarkFgColor;
                    foreach (TabPage tp in tc.TabPages)
                    {
                        tp.BackColor = DarkPanelBg;
                        tp.ForeColor = DarkFgColor;
                        foreach (Control c in tp.Controls) ApplyDark(c);
                    }
                    return;

                case Panel p:
                    p.BackColor = DarkPanelBg;
                    if (p.AutoScroll && p.IsHandleCreated)
                        NativeControlTheme(p.Handle, true);
                    foreach (Control c in p.Controls) ApplyDark(c);
                    return;

                default:
                    ctrl.BackColor = DarkPanelBg;
                    ctrl.ForeColor = DarkFgColor;
                    foreach (Control c in ctrl.Controls) ApplyDark(c);
                    return;
            }
        }

        // ── Light ────────────────────────────────────────────────────────────
        private static void ApplyLight(Control ctrl)
        {
            switch (ctrl)
            {
                case MenuStrip ms:
                    ms.ResetBackColor(); ms.ResetForeColor();
                    ms.Renderer = new ToolStripProfessionalRenderer();
                    ApplyMenuItems(ms.Items, dark: false);
                    return;

                case StatusStrip ss:
                    ss.ResetBackColor(); ss.ResetForeColor();
                    ss.Renderer = new ToolStripProfessionalRenderer();
                    foreach (ToolStripItem i in ss.Items) { i.ResetBackColor(); i.ResetForeColor(); }
                    return;

                case ToolStrip ts:
                    ts.ResetBackColor(); ts.ResetForeColor();
                    ts.Renderer = new ToolStripProfessionalRenderer();
                    ApplyToolStripItems(ts.Items, dark: false);
                    return;

                case DataGridView dgv:
                    dgv.BackgroundColor = SystemColors.Window;
                    dgv.GridColor       = Color.FromArgb(220, 220, 220);
                    dgv.DefaultCellStyle.BackColor          = SystemColors.Window;
                    dgv.DefaultCellStyle.ForeColor          = SystemColors.WindowText;
                    dgv.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
                    dgv.DefaultCellStyle.SelectionForeColor = SystemColors.HighlightText;
                    dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 252);
                    dgv.ColumnHeadersDefaultCellStyle.BackColor          = Color.FromArgb(235, 235, 240);
                    dgv.ColumnHeadersDefaultCellStyle.ForeColor          = SystemColors.WindowText;
                    dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(235, 235, 240);
                    return;

                case ListView lv:
                    lv.BackColor = SystemColors.Window;
                    lv.ForeColor = SystemColors.WindowText;
                    if (lv.IsHandleCreated) NativeControlTheme(lv.Handle, false);
                    return;

                case TreeView tv:
                    tv.BackColor = SystemColors.Window;
                    tv.ForeColor = SystemColors.WindowText;
                    tv.LineColor = Color.Black;
                    if (tv.IsHandleCreated) NativeControlTheme(tv.Handle, false);
                    return;

                case ListBox lb:
                    lb.BackColor = SystemColors.Window;
                    lb.ForeColor = SystemColors.WindowText;
                    return;

                case TextBox tb:
                    tb.BackColor = SystemColors.Window;
                    tb.ForeColor = SystemColors.WindowText;
                    tb.BorderStyle = BorderStyle.Fixed3D;
                    if (tb.IsHandleCreated) NativeControlTheme(tb.Handle, false);
                    return;

                case NumericUpDown nud:
                    nud.BackColor = SystemColors.Window;
                    nud.ForeColor = SystemColors.WindowText;
                    return;

                case ComboBox cb:
                    cb.BackColor = SystemColors.Window;
                    cb.ForeColor = SystemColors.WindowText;
                    return;

                case CheckBox chk:
                    chk.ResetBackColor(); chk.ResetForeColor();
                    return;

                case RadioButton rb:
                    rb.ResetBackColor(); rb.ResetForeColor();
                    return;

                case Label lbl:
                    lbl.ResetBackColor(); lbl.ResetForeColor();
                    return;

                case Button btn:
                    btn.ResetBackColor(); btn.ResetForeColor();
                    btn.FlatStyle = FlatStyle.Standard;
                    return;

                case PictureBox pb:
                    pb.ResetBackColor();
                    return;

                case SplitContainer sc:
                    sc.ResetBackColor();
                    sc.Panel1.ResetBackColor();
                    sc.Panel2.ResetBackColor();
                    foreach (Control c in sc.Panel1.Controls) ApplyLight(c);
                    foreach (Control c in sc.Panel2.Controls) ApplyLight(c);
                    return;

                case GroupBox gb:
                    gb.ResetBackColor(); gb.ResetForeColor();
                    foreach (Control c in gb.Controls) ApplyLight(c);
                    return;

                case TabControl tc:
                    tc.ResetBackColor(); tc.ResetForeColor();
                    foreach (TabPage tp in tc.TabPages)
                    {
                        tp.ResetBackColor(); tp.ResetForeColor();
                        foreach (Control c in tp.Controls) ApplyLight(c);
                    }
                    return;

                case Panel p:
                    p.ResetBackColor();
                    if (p.AutoScroll && p.IsHandleCreated)
                        NativeControlTheme(p.Handle, false);
                    foreach (Control c in p.Controls) ApplyLight(c);
                    return;

                default:
                    ctrl.ResetBackColor(); ctrl.ResetForeColor();
                    foreach (Control c in ctrl.Controls) ApplyLight(c);
                    return;
            }
        }

        // ── ListView column header dark drawing ───────────────────────────────
        private static void EnsureDarkColumnHeaders(ListView lv)
        {
            if (_managedListViews.Contains(lv)) return;
            _managedListViews.Add(lv);

            if (!lv.OwnerDraw)
            {
                lv.OwnerDraw = true;
                lv.DrawItem    += (_, e) => e.DrawDefault = true;
                lv.DrawSubItem += (_, e) => { e.DrawDefault = true; };
            }
            lv.DrawColumnHeader += DrawColumnHeader;
            lv.Disposed += (_, _) => _managedListViews.Remove(lv);
        }

        internal static void DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            if (Current != AppTheme.Dark) { e.DrawDefault = true; return; }

            using var bg = new SolidBrush(DarkHdrBg);
            e.Graphics.FillRectangle(bg, e.Bounds);

            using var line = new Pen(DarkHdrLine);
            e.Graphics.DrawLine(line, e.Bounds.Right - 1, e.Bounds.Top,
                                      e.Bounds.Right - 1, e.Bounds.Bottom - 1);
            e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1,
                                      e.Bounds.Right, e.Bounds.Bottom - 1);

            var textRect = new Rectangle(e.Bounds.X + 6, e.Bounds.Y,
                                          e.Bounds.Width - 10, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "",
                e.Font ?? SystemFonts.DefaultFont, textRect,
                Color.FromArgb(200, 200, 200),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        // ── ToolStrip/Menu item helpers ───────────────────────────────────────
        private static void ApplyMenuItems(ToolStripItemCollection items, bool dark)
        {
            foreach (ToolStripItem item in items)
            {
                if (dark) { item.BackColor = DarkMenuBg; item.ForeColor = DarkFgColor; }
                else      { item.ResetBackColor(); item.ResetForeColor(); }
                if (item is ToolStripMenuItem mi && mi.HasDropDownItems)
                    ApplyMenuItems(mi.DropDownItems, dark);
            }
        }

        private static void ApplyToolStripItems(ToolStripItemCollection items, bool dark)
        {
            foreach (ToolStripItem item in items)
            {
                switch (item)
                {
                    case ToolStripComboBox tscb:
                        if (dark)
                        {
                            tscb.BackColor = DarkInputBg; tscb.ForeColor = DarkFgColor;
                            if (tscb.ComboBox is not null)
                            { tscb.ComboBox.BackColor = DarkInputBg; tscb.ComboBox.ForeColor = DarkFgColor; }
                        }
                        else
                        {
                            tscb.ResetBackColor(); tscb.ResetForeColor();
                            if (tscb.ComboBox is not null)
                            { tscb.ComboBox.BackColor = SystemColors.Window; tscb.ComboBox.ForeColor = SystemColors.WindowText; }
                        }
                        break;

                    default:
                        if (dark) { item.BackColor = DarkStripBg; item.ForeColor = DarkFgColor; }
                        else      { item.ResetBackColor(); item.ResetForeColor(); }
                        break;
                }
            }
        }
    }

    // ── Dark renderer ────────────────────────────────────────────────────────
    public sealed class DarkStripRenderer : ToolStripRenderer
    {
        private static readonly Color BgColor   = Color.FromArgb(37, 37, 38);
        private static readonly Color MenuBg    = Color.FromArgb(45, 45, 48);
        private static readonly Color SelBg     = Color.FromArgb(62, 62, 64);
        private static readonly Color SelBorder = Color.FromArgb(80, 80, 80);
        private static readonly Color SepColor  = Color.FromArgb(68, 68, 68);
        private static readonly Color ImgMargin = Color.FromArgb(50, 50, 50);
        private static readonly Color FgEnabled = Color.FromArgb(212, 212, 212);
        private static readonly Color FgDisabled= Color.FromArgb(100, 100, 100);

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            if (ThemeManager.Current != AppTheme.Dark) return;
            using var b = new SolidBrush(BgColor);
            e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            if (ThemeManager.Current != AppTheme.Dark) return;
            using var b = new SolidBrush(ImgMargin);
            e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (ThemeManager.Current != AppTheme.Dark) { base.OnRenderMenuItemBackground(e); return; }
            var bg = e.Item.Selected ? SelBg : MenuBg;
            using var b = new SolidBrush(bg);
            e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, e.Item.Size));
            if (e.Item.Selected)
            {
                using var p = new Pen(SelBorder);
                e.Graphics.DrawRectangle(p, new Rectangle(0, 0, e.Item.Width - 1, e.Item.Height - 1));
            }
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            if (ThemeManager.Current != AppTheme.Dark) { base.OnRenderButtonBackground(e); return; }
            var bg = (e.Item.Selected || (e.Item is ToolStripButton b2 && b2.Checked)) ? SelBg : BgColor;
            using var b = new SolidBrush(bg);
            e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, e.Item.Size));
        }

        protected override void OnRenderLabelBackground(ToolStripItemRenderEventArgs e)
        {
            if (ThemeManager.Current != AppTheme.Dark) { base.OnRenderLabelBackground(e); return; }
            using var b = new SolidBrush(BgColor);
            e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, e.Item.Size));
        }

        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
        {
            if (ThemeManager.Current != AppTheme.Dark) { base.OnRenderDropDownButtonBackground(e); return; }
            var bg = e.Item.Selected ? SelBg : BgColor;
            using var b = new SolidBrush(bg);
            e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, e.Item.Size));
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            if (ThemeManager.Current != AppTheme.Dark) { base.OnRenderSeparator(e); return; }

            using var bg = new SolidBrush(BgColor);
            e.Graphics.FillRectangle(bg, new Rectangle(Point.Empty, e.Item.Size));

            using var pen = new Pen(SepColor);
            if (e.Item.IsOnDropDown)
            {
                // Horizontal rule for menu dropdowns
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(pen, 28, y, e.Item.Width - 4, y);
            }
            else
            {
                // Vertical rule for toolbars
                int x = e.Item.Width / 2;
                e.Graphics.DrawLine(pen, x, 4, x, e.Item.Height - 4);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (ThemeManager.Current == AppTheme.Dark)
                e.TextColor = e.Item.Enabled ? FgEnabled : FgDisabled;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            if (ThemeManager.Current == AppTheme.Dark)
                e.ArrowColor = Color.FromArgb(180, 180, 180);
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            if (ThemeManager.Current != AppTheme.Dark) { base.OnRenderItemCheck(e); return; }
            using var p = new Pen(Color.FromArgb(0, 122, 204), 1.5f);
            e.Graphics.DrawRectangle(p, e.ImageRectangle);
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
            => base.OnRenderItemImage(e);

        protected override void OnRenderGrip(ToolStripGripRenderEventArgs e) { }
    }
}
