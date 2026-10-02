using System.Text.Json;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms;

public sealed class FileManagerForm : Form
{
    private const string ModuleFile = "mullvad.Module.FileManager";
    private const string ModuleId   = "mullvad.filemanager";

    private readonly ClientHandler _handler;
    private ModuleContext?         _ctx;
    private string                 _currentPath = "";
    private readonly Stack<string> _history     = new();

    // ── Controls ─────────────────────────────────────────────────────────────
    private ToolStrip            toolbar        = null!;
    private ToolStripButton      btnBack        = null!;
    private ToolStripButton      btnUp          = null!;
    private ToolStripButton      btnRefresh     = null!;
    private ToolStripButton      btnDownload    = null!;
    private ToolStripButton      btnUpload      = null!;
    private ToolStripButton      btnDelete      = null!;
    private ToolStripButton      btnNewFolder   = null!;
    private ToolStripButton      btnOpenShell   = null!;
    private Panel                addressPanel   = null!;
    private TextBox              txtPath        = null!;
    private Button               btnGo          = null!;
    private SplitContainer       splitMain      = null!;
    private SplitContainer       splitLeft      = null!;
    private ListView             driveList      = null!;
    private Panel                infoPanel      = null!;
    private Panel                filterPanel    = null!;
    private TextBox              txtFilter      = null!;
    private ListView             listView       = null!;
    private List<ListViewItem>   _allItems      = new();
    private StatusStrip          statusStrip    = null!;
    private ToolStripStatusLabel statusLabel    = null!;
    private ToolStripProgressBar statusProgress = null!;
    private Panel                loadingPanel   = null!;
    private Label                loadingLabel   = null!;

    public FileManagerForm(ClientHandler handler)
    {
        _handler = handler;
        Build();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Form construction
    // ─────────────────────────────────────────────────────────────────────────
    private void Build()
    {
        SuspendLayout();

        Text          = $"File Manager  —  {_handler.Info.Computer}";
        ClientSize    = new Size(1080, 600);
        Font          = new Font("Segoe UI", 9F);
        MinimumSize   = new Size(700, 420);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = true;

        // Form icon — mullvad
        var ico = IconLoader.Load("mullvad.ico") as Bitmap
               ?? IconLoader.Load("mullvad-vpn.ico") as Bitmap;
        if (ico is not null) try { Icon = Icon.FromHandle(ico.GetHicon()); } catch { }

        // ── Toolbar ───────────────────────────────────────────────────────────
        toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };
        btnBack      = MakeBtn("◄", "Back",       "back.png");
        btnUp        = MakeBtn("↑", "Up",         "arrow_up.png");
        btnRefresh   = MakeBtn("⟳", "Refresh",    "refresh.png");
        btnDownload  = MakeBtn("⬇", "Download",   "arrow_down.png");
        btnUpload    = MakeBtn("⬆", "Upload",     "arrow_up.png");
        btnDelete    = MakeBtn("✗", "Delete",     "delete.png");
        btnNewFolder = MakeBtn("📁", "New Folder", "folder.png");
        btnOpenShell = MakeBtn("⊞", "Open in Terminal", "application_go.png");

        btnBack.Click      += (_, _) => NavigateBack();
        btnUp.Click        += (_, _) => NavigateUp();
        btnRefresh.Click   += (_, _) => _ = RefreshAsync();
        btnDownload.Click  += (_, _) => _ = DownloadAsync();
        btnUpload.Click    += (_, _) => _ = UploadAsync();
        btnDelete.Click    += (_, _) => _ = DeleteAsync();
        btnNewFolder.Click += (_, _) => _ = NewFolderAsync();
        btnOpenShell.Click += (_, _) =>
        {
            var frm = new RemoteShellForm(_handler, _currentPath);
            ThemeManager.ApplyForm(frm);
            frm.Show(this);
        };

        toolbar.Items.AddRange(new ToolStripItem[]
        {
            btnBack, btnUp, btnRefresh, new ToolStripSeparator(),
            btnDownload, btnUpload, btnDelete, btnNewFolder,
            new ToolStripSeparator(), btnOpenShell,
        });

        // ── Address bar ───────────────────────────────────────────────────────
        addressPanel = new Panel { Dock = DockStyle.Top, Height = 28, Padding = new Padding(4, 2, 4, 2) };
        txtPath = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F) };
        txtPath.KeyDown += (_, e) => { if (e.KeyCode == Keys.Return) _ = NavigateToAsync(txtPath.Text.Trim()); };
        btnGo = new Button { Text = "Go", Dock = DockStyle.Right, Width = 40, FlatStyle = FlatStyle.Flat };
        btnGo.Click += (_, _) => _ = NavigateToAsync(txtPath.Text.Trim());
        addressPanel.Controls.Add(txtPath);
        addressPanel.Controls.Add(btnGo);

        // ── Status strip ──────────────────────────────────────────────────────
        statusStrip    = new StatusStrip();
        statusLabel    = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        statusProgress = new ToolStripProgressBar { Visible = false, Width = 120 };
        statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, statusProgress });

        // ── Drive list (left panel) ───────────────────────────────────────────
        driveList = new ListView
        {
            View             = View.List,
            FullRowSelect    = true,
            MultiSelect      = false,
            ShowItemToolTips = true,
            Font             = new Font("Segoe UI", 9F),
            BorderStyle      = BorderStyle.FixedSingle,
        };
        var driveImgList = new ImageList { ImageSize = new Size(20, 20), ColorDepth = ColorDepth.Depth32Bit };
        driveList.SmallImageList = driveImgList;
        driveList.ItemActivate  += OnDriveActivate;
        driveList.Click         += OnDriveActivate;
        driveList.MouseDown     += (_, e) =>
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = driveList.HitTest(e.X, e.Y);
            if (hit.Item != null) { hit.Item.Selected = true; hit.Item.Focused = true; }
        };
        BuildDriveContextMenu();

        // ── File list view (right panel) ──────────────────────────────────────
        listView = new ListView
        {
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = false,
            MultiSelect   = true,
            Font          = new Font("Segoe UI", 9F),
        };
        listView.Columns.Add("Name",     260);
        listView.Columns.Add("Size",      90, HorizontalAlignment.Right);
        listView.Columns.Add("Type",     110);
        listView.Columns.Add("Modified", 160);

        var imgList = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
        imgList.Images.Add("folder", IconLoader.Load("folder.png") ?? SystemIcons.Shield.ToBitmap());
        imgList.Images.Add("file",   IconLoader.Load("file.png")   ?? SystemIcons.WinLogo.ToBitmap());
        listView.SmallImageList = imgList;
        listView.DoubleClick          += OnListDoubleClick;
        listView.SelectedIndexChanged += OnSelectionChanged;

        BuildContextMenu();

        listView.AllowDrop = true;
        listView.DragEnter += (_, e) =>
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop) && _ctx != null)
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        };
        listView.DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                _ = UploadDroppedFilesAsync(files);
        };
        listView.ItemDrag += OnItemDrag;

        // ── Info panel (bottom of left side) ─────────────────────────────────
        infoPanel = new Panel
        {
            Dock        = DockStyle.Fill,
            AutoScroll  = true,
            BackColor   = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
            Padding     = new Padding(6, 6, 6, 6),
        };

        // ── Inner vertical split (drives top / info bottom) ───────────────────
        splitLeft = new SplitContainer
        {
            Dock          = DockStyle.Fill,
            Orientation   = Orientation.Horizontal,
            Panel1MinSize = 60,
            Panel2MinSize = 60,
            SplitterWidth = 4,
        };
        splitLeft.Panel1.Controls.Add(driveList);
        driveList.Dock = DockStyle.Fill;
        splitLeft.Panel2.Controls.Add(infoPanel);

        // ── Main horizontal split (left panel / file list) ────────────────────
        splitMain = new SplitContainer
        {
            Dock          = DockStyle.Fill,
            Orientation   = Orientation.Vertical,
            Panel1MinSize = 120,
            SplitterWidth = 4,
        };
        splitMain.Panel1.Controls.Add(splitLeft);
        splitLeft.Dock = DockStyle.Fill;

        // ── Filter box above the file list ────────────────────────────────────
        filterPanel = new Panel { Dock = DockStyle.Top, Height = 28, Padding = new Padding(4, 2, 4, 2) };
        txtFilter = new TextBox
        {
            Dock            = DockStyle.Fill,
            Font            = new Font("Segoe UI", 9F),
            PlaceholderText = "Search…",
        };
        txtFilter.TextChanged += (_, _) => ApplyFilter();
        filterPanel.Controls.Add(txtFilter);
        listView.Dock = DockStyle.Fill;
        splitMain.Panel2.Controls.Add(listView);

        // ── Loading overlay ───────────────────────────────────────────────────
        loadingPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(200, SystemColors.Control), Visible = true };
        loadingLabel = new Label
        {
            Text = "Delivering module to client…",
            AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10F),
        };
        loadingPanel.Controls.Add(loadingLabel);

        // Add in correct dock order — last added Fill wins for display purposes
        Controls.Add(splitMain);
        Controls.Add(addressPanel);
        Controls.Add(filterPanel);
        Controls.Add(toolbar);
        Controls.Add(statusStrip);
        Controls.Add(loadingPanel);
        loadingPanel.BringToFront();

        ResumeLayout(false);
        PerformLayout();

        Load      += OnLoad;
        FormClosed += OnFormClosed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Context menu
    // ─────────────────────────────────────────────────────────────────────────
    private ToolStripMenuItem miDownload  = null!;
    private ToolStripMenuItem miDelete    = null!;
    private ToolStripMenuItem mi7zAddTo   = null!;
    private ToolStripMenuItem mi7zAddTo7z = null!;
    private ToolStripMenuItem mi7zAddZip  = null!;
    private ToolStripMenuItem mi7zDl      = null!;
    private ToolStripMenuItem miEncrypt   = null!;
    private ToolStripMenuItem miEncryptPw = null!;
    private ToolStripMenuItem miDecrypt   = null!;
    private ToolStripMenuItem miDecryptPw = null!;

    private void BuildContextMenu()
    {
        var ctx = new ContextMenuStrip();

        miDownload = MakeMenuItem("Download",       "arrow_down.png", (_, _) => _ = DownloadAsync());
        var miUpload = MakeMenuItem("Upload here",  "arrow_up.png",   (_, _) => _ = UploadAsync());
        miDelete   = MakeMenuItem("Delete",         "delete.png",     (_, _) => _ = DeleteAsync());
        var miNew  = MakeMenuItem("New Folder",     "folder.png",     (_, _) => _ = NewFolderAsync());

        // 7-Zip submenu
        var mi7z = new ToolStripMenuItem("7-Zip") { Image = IconLoader.Load("archive.png") };
        mi7zAddTo   = new ToolStripMenuItem("Add to archive…");
        mi7zAddTo7z = new ToolStripMenuItem("Add to \"{name}.7z\"");
        mi7zAddZip  = new ToolStripMenuItem("Add to \"{name}.zip\"");
        mi7zDl      = new ToolStripMenuItem("Compress and Download");
        mi7z.DropDownItems.AddRange(new ToolStripItem[] { mi7zAddTo, mi7zAddTo7z, mi7zAddZip, new ToolStripSeparator(), mi7zDl });
        mi7zAddTo.Click   += (_, _) => _ = ZipAsync(promptName: true,  ext: ".zip", download: false);
        mi7zAddTo7z.Click += (_, _) => _ = ZipAsync(promptName: false, ext: ".7z",  download: false);
        mi7zAddZip.Click  += (_, _) => _ = ZipAsync(promptName: false, ext: ".zip", download: false);
        mi7zDl.Click      += (_, _) => _ = ZipAsync(promptName: false, ext: ".zip", download: true);

        // Encryption submenu
        var miEncMenu = new ToolStripMenuItem("Encryption") { Image = IconLoader.Load("key_go.png") };
        miEncrypt   = new ToolStripMenuItem("Encrypt…");
        miEncryptPw = new ToolStripMenuItem("Encrypt with password");
        miEncrypt.Click   += (_, _) => _ = CryptAsync(encrypt: true,  withPassword: false);
        miEncryptPw.Click += (_, _) => _ = CryptAsync(encrypt: true,  withPassword: true);
        miEncMenu.DropDownItems.AddRange(new ToolStripItem[] { miEncrypt, miEncryptPw });

        // Decryption submenu
        var miDecMenu = new ToolStripMenuItem("Decryption") { Image = IconLoader.Load("decrypt.ico") };
        miDecrypt   = new ToolStripMenuItem("Decrypt…");
        miDecryptPw = new ToolStripMenuItem("Decrypt with password");
        miDecrypt.Click   += (_, _) => _ = CryptAsync(encrypt: false, withPassword: false);
        miDecryptPw.Click += (_, _) => _ = CryptAsync(encrypt: false, withPassword: true);
        miDecMenu.DropDownItems.AddRange(new ToolStripItem[] { miDecrypt, miDecryptPw });

        ctx.Items.Add(miDownload);
        ctx.Items.Add(miUpload);
        ctx.Items.Add(new ToolStripSeparator());
        ctx.Items.Add(miDelete);
        ctx.Items.Add(miNew);
        ctx.Items.Add(new ToolStripSeparator());
        ctx.Items.Add(mi7z);
        ctx.Items.Add(miEncMenu);
        ctx.Items.Add(miDecMenu);

        ctx.Opening += (_, _) =>
        {
            bool hasSel = listView.SelectedItems.Count > 0 &&
                          listView.SelectedItems.Cast<ListViewItem>().Any(i => i.Text != "..");
            miDownload.Enabled = hasSel;
            miDelete.Enabled   = hasSel;
            mi7z.Enabled       = hasSel;
            mi7zDl.Enabled     = hasSel;
            miEncMenu.Enabled  = hasSel;
            miDecMenu.Enabled  = hasSel;
            // Update dynamic label based on selection
            UpdateZipLabels();
        };
        listView.ContextMenuStrip = ctx;
    }

    private void UpdateZipLabels()
    {
        var items = SelectedRemotePaths();
        string baseName = items.Count == 1
            ? Path.GetFileNameWithoutExtension(items[0])
            : "archive";
        mi7zAddTo7z.Text = $"Add to \"{baseName}.7z\"";
        mi7zAddZip.Text  = $"Add to \"{baseName}.zip\"";
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Form load — deliver module then show UI
    // ─────────────────────────────────────────────────────────────────────────
    private async void OnLoad(object? sender, EventArgs e)
    {
        // Set after layout so dimensions are valid
        try { splitMain.Panel2MinSize = 300; splitMain.SplitterDistance = 175; } catch { }
        try { splitLeft.SplitterDistance = splitLeft.Height / 2; } catch { }

        SetToolbarEnabled(false);
        _handler.Disconnected += OnClientDisconnected;

        var progress = new Progress<string>(msg => { loadingLabel.Text = msg; statusLabel.Text = msg; });
        var bytes = ModuleLoader.GetModuleBytes(ModuleFile);
        if (bytes == null)
        {
            loadingLabel.Text = "Module file not found.\nBuild the FileManager project first.";
            return;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        bool ok = await ModuleDelivery.EnsureDeliveredAsync(_handler, ModuleFile, bytes, progress, cts.Token);
        if (!ok) { loadingLabel.Text = "Failed to load module on client."; return; }

        _ctx = new ModuleContext(_handler, ModuleId);
        _ctx.Disconnected += (_, _) => BeginInvoke(() => OnClientDisconnected(_handler));

        loadingPanel.Visible = false;
        SetToolbarEnabled(true);
        statusLabel.Text = "Ready.";

        await LoadDrivesAsync();
        await NavigateToAsync(@"C:\");
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        _handler.Disconnected -= OnClientDisconnected;
        _ctx?.Dispose();
    }

    private void OnClientDisconnected(ClientHandler _)
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            SetToolbarEnabled(false);
            statusLabel.Text  = "Disconnected.";
            loadingLabel.Text = "Client disconnected.";
            loadingPanel.Visible = true;
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Drive panel
    // ─────────────────────────────────────────────────────────────────────────
    private async Task LoadDrivesAsync()
    {
        if (_ctx == null) return;
        try
        {
            var json = await _ctx.ExecuteAsync("getdrives", "");
            var doc  = JsonDocument.Parse(json).RootElement;
            if (doc.ValueKind != JsonValueKind.Array) return;

            var imgList = driveList.SmallImageList!;
            driveList.Items.Clear();
            imgList.Images.Clear();

            foreach (var d in doc.EnumerateArray())
            {
                var name  = Str(d, "name").TrimEnd('\\', '/');   // e.g. "C:"
                var label = Str(d, "label");
                var avail = Str(d, "avail_gb");
                var total = Str(d, "total_gb");

                string letter  = name.Length > 0 ? name[0].ToString().ToUpper() : "";
                string icoName = letter == "C" ? "Icon_C.ico" : "Icon_D.ico";
                var img = IconLoader.Load(icoName) ?? IconLoader.Load("drive_go.png");

                string imgKey = "drive_" + letter;
                if (img != null && !imgList.Images.ContainsKey(imgKey))
                    imgList.Images.Add(imgKey, img);

                bool ready = d.TryGetProperty("ready", out var rv) && rv.GetBoolean();
                string display = string.IsNullOrEmpty(label) ? name : $"{name}  {label}";
                var lvi = new ListViewItem(display)
                {
                    Tag          = name + @"\",
                    ImageKey     = imgKey,
                    ToolTipText  = ready ? $"{avail} GB free of {total} GB" : "Not ready",
                    ForeColor    = ready ? driveList.ForeColor : Color.Gray,
                };
                driveList.Items.Add(lvi);
            }
        }
        catch { }
    }

    private void OnDriveActivate(object? sender, EventArgs e)
    {
        if (driveList.SelectedItems.Count == 0) return;
        var path = driveList.SelectedItems[0].Tag as string;
        if (!string.IsNullOrEmpty(path))
            _ = NavigateToAsync(path);
    }

    private void BuildDriveContextMenu()
    {
        static ToolStripMenuItem Dead(string text)
            => new ToolStripMenuItem(text) { Enabled = false };

        var ctx = new ContextMenuStrip();

        // ── Information ───────────────────────────────────────────────────────
        var miInfo = new ToolStripMenuItem("Information") { Image = IconLoader.Load("information.png") };
        void AddInfo(string label, string? icon, string action, string title)
        {
            var mi = new ToolStripMenuItem(label) { Image = icon is null ? null : IconLoader.Load(icon) };
            mi.Click += (_, _) => _ = DriveActionAsync(action, title);
            miInfo.DropDownItems.Add(mi);
        }
        AddInfo("Disk Information",          "drive_go.png",   "disk_info",          "Disk Information");
        AddInfo("Advanced Disk Information", "bricks.png",     "disk_info_advanced", "Advanced Disk Information");
        AddInfo("Storage Usage",             "monitoring.png", "storage_usage",      "Storage Usage");
        AddInfo("Encryption Status",         "key_go.png",     "encryption_status",  "Encryption Status");

        // ── Storage (greyed out) ──────────────────────────────────────────────
        var miStorage = Dead("Storage");
        miStorage.Image = IconLoader.Load("archive.png");
        miStorage.DropDownItems.AddRange(new ToolStripItem[]
        {
            Dead("Find Largest Files"), Dead("Find Largest Folders"),
            Dead("Find Duplicate Files"), Dead("Find Empty Folders"),
            new ToolStripSeparator(),
            Dead("Clear Temp"), Dead("Clear Cache"),
        });

        // ── Permissions ───────────────────────────────────────────────────────
        var miPerms = new ToolStripMenuItem("Permissions") { Image = IconLoader.Load("uac_shield.png") ?? IconLoader.Load("user.png") };
        void AddPerm(string label, string? icon, string action, string title)
        {
            var mi = new ToolStripMenuItem(label) { Image = icon is null ? null : IconLoader.Load(icon) };
            mi.Click += (_, _) => _ = DriveActionAsync(action, title);
            miPerms.DropDownItems.Add(mi);
        }
        AddPerm("View Permissions", "uac_shield.png", "acl_view",   "View Permissions");
        AddPerm("View Owner",       "user.png",       "acl_owner",  "View Owner");
        AddPerm("Export ACL",       "save.png",       "acl_export", "Export ACL");

        // ── Search (greyed out) ───────────────────────────────────────────────
        var miSearch = Dead("Search");
        miSearch.Image = IconLoader.Load("keyboard_magnify.png");
        var miSearchBasic = Dead("Search");
        miSearchBasic.DropDownItems.AddRange(new ToolStripItem[]
            { Dead("Search for File"), Dead("Search for Folder"), Dead("Search for Application") });
        var miSearchAdv = Dead("Advanced Search");
        miSearchAdv.DropDownItems.AddRange(new ToolStripItem[]
        {
            Dead("Search Entire Drive"), Dead("Search by Name"), Dead("Search by Extension"),
            Dead("Search by Size"), Dead("Search by Date"), Dead("Search by Attributes"),
            Dead("Search File Contents"),
        });
        miSearch.DropDownItems.Add(miSearchBasic);
        miSearch.DropDownItems.Add(miSearchAdv);

        // ── Encryption (greyed out) ───────────────────────────────────────────
        var miEnc = Dead("Encryption");
        miEnc.Image = IconLoader.Load("key_go.png");
        var miEncBasic = Dead("Encryption");
        miEncBasic.DropDownItems.AddRange(new ToolStripItem[]
        {
            Dead("Start"), Dead("Stop"), new ToolStripSeparator(),
            Dead("Encryption Status"), Dead("Encryption Settings"), Dead("Recovery Key"),
        });
        var miEncAdv = Dead("Advanced Encryption");
        miEncAdv.DropDownItems.AddRange(new ToolStripItem[]
            { Dead("Encrypt certain Files"), Dead("Encrypt certain Folders"), Dead("Encryption Diagnostics") });
        miEnc.DropDownItems.Add(miEncBasic);
        miEnc.DropDownItems.Add(miEncAdv);

        ctx.Items.AddRange(new ToolStripItem[]
        {
            miInfo, miStorage, miPerms, miSearch, miEnc,
        });

        driveList.ContextMenuStrip = ctx;
    }

    private async Task DriveActionAsync(string action, string title)
    {
        if (_ctx == null || driveList.SelectedItems.Count == 0) return;
        var drivePath = driveList.SelectedItems[0].Tag as string ?? "";
        statusLabel.Text = $"{title}…";
        try
        {
            var json = await _ctx.ExecuteAsync(action, drivePath, timeout: TimeSpan.FromSeconds(30));
            var doc  = JsonDocument.Parse(json).RootElement;
            string content;
            if (doc.TryGetProperty("error", out var err))
                content = "Error:\n\n" + err.GetString();
            else if (doc.TryGetProperty("text", out var txt))
                content = txt.GetString() ?? "";
            else content = json;
            ShowInfoWindow(title + "  —  " + drivePath, content);
            statusLabel.Text = "Ready.";
        }
        catch (Exception ex) { statusLabel.Text = $"Error: {ex.Message}"; }
    }

    private static void ShowInfoWindow(string title, string content)
    {
        var frm = new Form
        {
            Text          = title,
            ClientSize    = new Size(600, 520),
            MinimumSize   = new Size(440, 320),
            Font          = new Font("Segoe UI", 9F),
            StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false,
        };

        // Icon
        var ico = IconLoader.Load("drive_go.png") as Bitmap;
        if (ico is not null) try { frm.Icon = Icon.FromHandle(ico.GetHicon()); } catch { frm.Icon = SystemIcons.Information; }
        else frm.Icon = SystemIcons.Information;

        var lv = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = false,
            HeaderStyle   = ColumnHeaderStyle.Nonclickable,
            MultiSelect   = false,
            ShowGroups    = true,
            BorderStyle   = BorderStyle.None,
        };
        lv.Columns.Add("Property", 185);
        lv.Columns.Add("Value",    380);

        frm.Controls.Add(lv);

        ListViewGroup? currentGroup = null;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;

            var stripped = line.Trim(' ', '─', '━', '═', '-');
            if (stripped.Length == 0 || (double)stripped.Length / line.Length < 0.25) continue;

            bool isHdr = line.TrimStart().StartsWith("──") || line.TrimStart().StartsWith("--")
                      || line.TrimStart().StartsWith("===") || line.TrimStart().StartsWith("***");
            if (isHdr || !line.Contains(':'))
            {
                var text = line.Trim().Trim('─', '-', '=', '*', ' ');
                if (string.IsNullOrEmpty(text)) continue;
                currentGroup = new ListViewGroup(text, HorizontalAlignment.Left);
                lv.Groups.Add(currentGroup);
                continue;
            }

            var colon = line.IndexOf(':');
            var k = line[..colon].Trim();
            var v = line[(colon + 1)..].Trim();

            var item = new ListViewItem(k) { Group = currentGroup };
            item.SubItems.Add(v);
            lv.Items.Add(item);
        }

        lv.Resize += (_, _) =>
        {
            int w = lv.ClientSize.Width - lv.Columns[0].Width - SystemInformation.VerticalScrollBarWidth - 4;
            if (w > 60) lv.Columns[1].Width = w;
        };

        frm.Show();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Navigation
    // ─────────────────────────────────────────────────────────────────────────
    private async Task NavigateToAsync(string path, bool pushHistory = true)
    {
        if (_ctx == null) return;
        if (pushHistory && _currentPath.Length > 0) _history.Push(_currentPath);
        _currentPath = path;
        txtPath.Text = path;
        await RefreshAsync();
    }

    private void NavigateBack()
    {
        if (_history.Count == 0) return;
        _ = NavigateToAsync(_history.Pop(), pushHistory: false);
    }

    private void NavigateUp()
    {
        if (string.IsNullOrEmpty(_currentPath)) return;
        var parent = Path.GetDirectoryName(_currentPath.TrimEnd('\\', '/'));
        _ = NavigateToAsync(parent ?? "");
    }

    private async Task RefreshAsync()
    {
        if (_ctx == null) return;
        SetBusy(true);
        try
        {
            var json = await _ctx.ExecuteAsync("list", _currentPath);
            PopulateList(json);
        }
        catch (OperationCanceledException) { statusLabel.Text = "Timed out."; }
        catch (Exception ex)              { statusLabel.Text = $"Error: {ex.Message}"; }
        finally { SetBusy(false); }
    }


    private void OnListDoubleClick(object? sender, EventArgs e)
    {
        if (listView.SelectedItems.Count == 0) return;
        var item  = listView.SelectedItems[0];
        bool isDir = item.Tag as bool? == true;
        if (isDir)
        {
            if (item.Text == "..")
            {
                NavigateUp();
                return;
            }
            var path = string.IsNullOrEmpty(_currentPath) ? item.Text : Path.Combine(_currentPath, item.Text);
            _ = NavigateToAsync(path);
        }
        else _ = DownloadAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Populate list
    // ─────────────────────────────────────────────────────────────────────────
    private void PopulateList(string json)
    {
        txtFilter.Text = "";
        _allItems.Clear();
        ClearInfoPanel();
        try
        {
            if (!string.IsNullOrEmpty(_currentPath))
            {
                var up = new ListViewItem("..") { Tag = (object)true, ImageKey = "folder" };
                up.SubItems.Add(""); up.SubItems.Add("Parent Folder"); up.SubItems.Add("");
                _allItems.Add(up);
            }

            var doc = JsonDocument.Parse(json).RootElement;

            if (doc.ValueKind == JsonValueKind.Object
                && doc.TryGetProperty("success", out var succ) && !succ.GetBoolean())
            {
                statusLabel.Text = "Error: " + (doc.TryGetProperty("error", out var e) ? e.GetString() : "Unknown");
                ApplyFilter();
                return;
            }

            if (doc.ValueKind == JsonValueKind.Array)
            {
                foreach (var d in doc.EnumerateArray())
                {
                    var name  = Str(d, "name");
                    var avail = Str(d, "avail_gb") + " GB free";
                    var type  = Str(d, "type");
                    var lvi   = new ListViewItem(name) { Tag = (object)true, ImageKey = "folder" };
                    lvi.SubItems.Add(avail); lvi.SubItems.Add($"Drive ({type})"); lvi.SubItems.Add("");
                    _allItems.Add(lvi);
                }
                statusLabel.Text = $"{_allItems.Count} drive(s)";
                ApplyFilter();
                return;
            }

            if (doc.TryGetProperty("entries", out var entries))
            {
                long totalSize = 0; int fileCount = 0, dirCount = 0;
                foreach (var e in entries.EnumerateArray())
                {
                    bool isDir  = e.TryGetProperty("is_dir", out var isd) && isd.GetBoolean();
                    var name    = Str(e, "name");
                    long rawSz  = isDir ? 0 : (e.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0);
                    var size    = isDir ? "" : FormatSize(rawSz);
                    var typStr  = isDir ? "Folder" : GetFileType(name);
                    var mod     = Str(e, "modified");
                    var lvi     = new ListViewItem(name) { Tag = (object)isDir, ImageKey = isDir ? "folder" : "file" };
                    var sizeItem = new ListViewItem.ListViewSubItem { Text = size, Tag = rawSz.ToString() };
                    lvi.SubItems.Add(sizeItem); lvi.SubItems.Add(typStr); lvi.SubItems.Add(mod);
                    _allItems.Add(lvi);
                    if (isDir) dirCount++;
                    else { fileCount++; totalSize += e.TryGetProperty("size", out var s2) ? s2.GetInt64() : 0; }
                }
                statusLabel.Text = $"{dirCount} folder(s), {fileCount} file(s), {FormatSize(totalSize)} total";
            }
        }
        catch (Exception ex) { statusLabel.Text = $"Parse error: {ex.Message}"; }
        finally
        {
            ApplyFilter();
            btnBack.Enabled = _history.Count > 0;
            btnUp.Enabled   = !string.IsNullOrEmpty(_currentPath);
        }
    }

    private void ApplyFilter()
    {
        var term = txtFilter.Text.Trim();
        listView.BeginUpdate();
        listView.Items.Clear();
        foreach (var item in _allItems)
        {
            if (term.Length == 0 || item.Text == ".." ||
                item.Text.Contains(term, StringComparison.OrdinalIgnoreCase))
                listView.Items.Add(item);
        }
        listView.EndUpdate();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  File operations
    // ─────────────────────────────────────────────────────────────────────────
    private async Task DownloadAsync()
    {
        if (_ctx == null || listView.SelectedItems.Count == 0) return;
        var item = listView.SelectedItems[0];
        if (item.Tag as bool? == true) return;

        var remPath = Path.Combine(_currentPath, item.Text);
        using var dlg = new SaveFileDialog { FileName = item.Text };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        SetBusy(true);
        statusLabel.Text = $"Downloading {item.Text}…";
        try
        {
            const int chunkSize = 2 * 1024 * 1024; // 2 MB per packet
            long offset = 0;
            using var fs = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write);
            while (true)
            {
                var payload = $"{{\"path\":{JsonString(remPath)},\"offset\":{offset},\"length\":{chunkSize}}}";
                var json    = await _ctx.ExecuteAsync("read_chunk", payload, timeout: TimeSpan.FromSeconds(60));
                var doc     = JsonDocument.Parse(json).RootElement;

                if (doc.TryGetProperty("error", out var errEl))
                {
                    statusLabel.Text = "Download failed: " + errEl.GetString();
                    return;
                }
                if (!doc.TryGetProperty("data", out var dataEl)) break;

                var bytes = Convert.FromBase64String(dataEl.GetString()!);
                await fs.WriteAsync(bytes);
                offset += bytes.Length;

                long total = doc.TryGetProperty("total", out var totEl) ? totEl.GetInt64() : 0;
                if (total > 0) statusLabel.Text = $"Downloading {item.Text}… {offset * 100 / total}%";

                bool done = doc.TryGetProperty("done", out var doneEl) && doneEl.GetBoolean();
                if (done || bytes.Length == 0) break;
            }
            statusLabel.Text = $"Downloaded {item.Text}.";
        }
        catch (Exception ex) { statusLabel.Text = $"Download error: {ex.Message}"; }
        finally { SetBusy(false); }
    }

    private async Task UploadAsync()
    {
        if (_ctx == null) return;
        using var dlg = new OpenFileDialog { Title = "Select file to upload" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var fileName   = Path.GetFileName(dlg.FileName);
        var remotePath = Path.Combine(_currentPath, fileName);

        SetBusy(true);
        statusLabel.Text = $"Uploading {fileName}…";
        try
        {
            const int chunkSize = 2 * 1024 * 1024; // 2 MB per packet
            var fileBytes = File.ReadAllBytes(dlg.FileName);
            long offset   = 0;
            while (offset < fileBytes.Length)
            {
                int take    = (int)Math.Min(chunkSize, fileBytes.Length - offset);
                var chunk   = new byte[take];
                Buffer.BlockCopy(fileBytes, (int)offset, chunk, 0, take);
                var payload = $"{{\"path\":{JsonString(remotePath)},\"offset\":{offset},\"data\":\"{Convert.ToBase64String(chunk)}\"}}";
                await _ctx.ExecuteAsync("write_chunk", payload, timeout: TimeSpan.FromSeconds(60));
                offset += take;
                statusLabel.Text = $"Uploading {fileName}… {offset * 100 / fileBytes.Length}%";
            }
            statusLabel.Text = "Upload complete.";
            await RefreshAsync();
        }
        catch (Exception ex) { statusLabel.Text = $"Upload error: {ex.Message}"; }
        finally { SetBusy(false); }
    }

    private async Task DeleteAsync()
    {
        if (_ctx == null || listView.SelectedItems.Count == 0) return;
        var item = listView.SelectedItems[0];
        if (item.Text == "..") return;

        var remPath = Path.Combine(_currentPath, item.Text);
        if (MessageBox.Show($"Delete '{item.Text}'?", "Confirm Delete",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        SetBusy(true);
        try { await _ctx.ExecuteAsync("delete", remPath); await RefreshAsync(); }
        catch (Exception ex) { statusLabel.Text = $"Delete error: {ex.Message}"; }
        finally { SetBusy(false); }
    }

    private async Task NewFolderAsync()
    {
        if (_ctx == null || string.IsNullOrEmpty(_currentPath)) return;
        var name = Prompt("New Folder", "Folder name:", "New Folder");
        if (string.IsNullOrWhiteSpace(name)) return;
        SetBusy(true);
        try { await _ctx.ExecuteAsync("mkdir", Path.Combine(_currentPath, name)); await RefreshAsync(); }
        catch (Exception ex) { statusLabel.Text = $"Error: {ex.Message}"; }
        finally { SetBusy(false); }
    }

    // ── 7-Zip operations ──────────────────────────────────────────────────────
    private async Task ZipAsync(bool promptName, string ext, bool download)
    {
        if (_ctx == null) return;
        var selected = SelectedRemotePaths();
        if (selected.Count == 0) return;

        string baseName = selected.Count == 1
            ? Path.GetFileNameWithoutExtension(selected[0])
            : "archive";

        string destName;
        if (promptName)
        {
            var n = Prompt("Add to archive", "Archive filename:", baseName + ext);
            if (string.IsNullOrWhiteSpace(n)) return;
            destName = n;
            if (!destName.EndsWith(ext, StringComparison.OrdinalIgnoreCase) &&
                !destName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                destName += ext;
        }
        else destName = baseName + ext;

        // Archives are created on the remote machine as ZIP regardless of extension label
        var destPath = Path.Combine(_currentPath, destName);
        var pathList = string.Join("\n", selected);
        var payload  = $"{{\"dest\":{JsonString(destPath)},\"paths\":{JsonString(pathList)}}}";

        SetBusy(true);
        statusLabel.Text = "Compressing…";
        try
        {
            await _ctx.ExecuteAsync("zip", payload);
            statusLabel.Text = $"Compressed to {destName}.";

            if (download)
            {
                using var dlg = new SaveFileDialog { FileName = destName, Filter = "ZIP|*.zip|All files|*.*" };
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    statusLabel.Text = "Downloading archive…";
                    var json = await _ctx.ExecuteAsync("read", destPath);
                    var doc  = JsonDocument.Parse(json).RootElement;
                    if (doc.TryGetProperty("data", out var data))
                    {
                        File.WriteAllBytes(dlg.FileName, Convert.FromBase64String(data.GetString()!));
                        statusLabel.Text = $"Downloaded {destName}.";
                    }
                }
            }
            await RefreshAsync();
        }
        catch (Exception ex) { statusLabel.Text = $"Compress error: {ex.Message}"; }
        finally { SetBusy(false); }
    }

    // ── Encryption / Decryption ───────────────────────────────────────────────
    private async Task CryptAsync(bool encrypt, bool withPassword)
    {
        if (_ctx == null) return;
        var selected = SelectedRemotePaths();
        if (selected.Count == 0) return;

        string action;
        string payload;

        if (withPassword)
        {
            var pw = Prompt(
                encrypt ? "Encrypt with password" : "Decrypt with password",
                "Password:");
            if (string.IsNullOrEmpty(pw)) return;
            action  = encrypt ? "encrypt_pw" : "decrypt_pw";
            var pathList = string.Join("\n", selected);
            payload = $"{{\"paths\":{JsonString(pathList)},\"password\":{JsonString(pw)}}}";
        }
        else
        {
            action  = encrypt ? "encrypt" : "decrypt";
            payload = string.Join("\n", selected);
        }

        SetBusy(true);
        statusLabel.Text = (encrypt ? "Encrypting" : "Decrypting") + "…";
        try
        {
            var json = await _ctx.ExecuteAsync(action, payload);
            var doc  = JsonDocument.Parse(json).RootElement;
            int ok   = doc.TryGetProperty("ok", out var v) ? v.GetInt32() : 0;
            statusLabel.Text = $"{(encrypt ? "Encrypted" : "Decrypted")} {ok} file(s).";
            await RefreshAsync();
        }
        catch (Exception ex) { statusLabel.Text = $"Error: {ex.Message}"; }
        finally { SetBusy(false); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Drag & drop
    // ─────────────────────────────────────────────────────────────────────────
    private async Task UploadDroppedFilesAsync(string[] localFiles)
    {
        if (_ctx == null || string.IsNullOrEmpty(_currentPath)) return;
        SetBusy(true);
        const int chunkSize = 2 * 1024 * 1024;
        int ok = 0;
        foreach (var localPath in localFiles)
        {
            if (Directory.Exists(localPath)) continue;
            try
            {
                var fileName   = Path.GetFileName(localPath);
                var remotePath = Path.Combine(_currentPath, fileName);
                var fileBytes  = File.ReadAllBytes(localPath);
                long offset    = 0;
                while (offset < fileBytes.Length)
                {
                    int take    = (int)Math.Min(chunkSize, fileBytes.Length - offset);
                    var chunk   = new byte[take];
                    Buffer.BlockCopy(fileBytes, (int)offset, chunk, 0, take);
                    var payload = $"{{\"path\":{JsonString(remotePath)},\"offset\":{offset},\"data\":\"{Convert.ToBase64String(chunk)}\"}}";
                    statusLabel.Text = $"Uploading {fileName}… {(offset + take) * 100 / fileBytes.Length}%";
                    await _ctx.ExecuteAsync("write_chunk", payload, timeout: TimeSpan.FromSeconds(60));
                    offset += take;
                }
                ok++;
            }
            catch { }
        }
        statusLabel.Text = $"Uploaded {ok}/{localFiles.Length} file(s).";
        SetBusy(false);
        await RefreshAsync();
    }

    private void OnItemDrag(object? sender, ItemDragEventArgs e)
    {
        var items = listView.SelectedItems.Cast<ListViewItem>()
                            .Where(i => i.Text != ".." && !(i.Tag as bool? == true))
                            .ToList();
        if (items.Count == 0 || _ctx == null) return;

        var tmpDir = Path.Combine(Path.GetTempPath(), "mullvad_drag_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tmpDir);

        var localFiles = new List<string>();
        foreach (var item in items)
        {
            try
            {
                var remotePath = Path.Combine(_currentPath, item.Text);
                var ctx  = _ctx;
                var json = Task.Run(() => ctx.ExecuteAsync("read", remotePath)).GetAwaiter().GetResult();
                var doc  = System.Text.Json.JsonDocument.Parse(json).RootElement;
                if (doc.TryGetProperty("data", out var d))
                {
                    var localPath = Path.Combine(tmpDir, item.Text);
                    File.WriteAllBytes(localPath, Convert.FromBase64String(d.GetString()!));
                    localFiles.Add(localPath);
                }
            }
            catch { }
        }
        if (localFiles.Count > 0)
            DoDragDrop(new DataObject(DataFormats.FileDrop, localFiles.ToArray()), DragDropEffects.Copy);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private List<string> SelectedRemotePaths()
        => listView.SelectedItems.Cast<ListViewItem>()
                   .Where(i => i.Text != "..")
                   .Select(i => Path.Combine(_currentPath, i.Text))
                   .ToList();

    private void SetToolbarEnabled(bool on)
    {
        foreach (ToolStripItem item in toolbar.Items) item.Enabled = on;
        txtPath.Enabled  = on;
        btnGo.Enabled    = on;
        listView.Enabled = on;
    }

    private void SetBusy(bool busy)
    {
        statusProgress.Visible = busy;
        if (busy) statusProgress.Style = ProgressBarStyle.Marquee;
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        var sel = listView.SelectedItems;
        bool hasSel = sel.Count > 0;
        btnDownload.Enabled = hasSel;
        btnDelete.Enabled   = hasSel;
        UpdateInfoPanel();
    }

    private void ClearInfoPanel()
    {
        infoPanel.SuspendLayout();
        foreach (Control c in infoPanel.Controls) c.Dispose();
        infoPanel.Controls.Clear();
        infoPanel.ResumeLayout();
    }

    private void UpdateInfoPanel()
    {
        infoPanel.SuspendLayout();
        foreach (Control c in infoPanel.Controls) c.Dispose();
        infoPanel.Controls.Clear();

        var sel = listView.SelectedItems;
        if (sel.Count == 0) { infoPanel.ResumeLayout(); return; }

        // Collect (key, value) rows — empty key signals a separator line
        var rows = new List<(string Key, string Val)>();

        if (sel.Count > 1)
        {
            int dirs = 0, files = 0; long total = 0;
            foreach (ListViewItem it in sel)
            {
                bool d = it.Tag as bool? == true;
                if (d) dirs++; else files++;
                if (!d && it.SubItems.Count > 1 && long.TryParse(it.SubItems[1].Tag as string, out var sz)) total += sz;
            }
            rows.Add(("Selected", $"{sel.Count} items"));
            if (files > 0) rows.Add(("Files",   $"{files}"));
            if (dirs  > 0) rows.Add(("Folders", $"{dirs}"));
            if (files > 0) rows.Add(("Size",    FormatSize(total)));
            rows.Add(("", ""));
            rows.Add(("Location", _currentPath));
        }
        else
        {
            var item   = sel[0];
            var name   = item.Text;
            bool isDir = item.Tag as bool? == true;
            var type   = isDir ? "Folder" : (item.SubItems.Count > 2 ? item.SubItems[2].Text : "File");
            var mod    = item.SubItems.Count > 3 ? item.SubItems[3].Text : "";
            long bytes = 0;
            if (item.SubItems.Count > 1) long.TryParse(item.SubItems[1].Tag as string, out bytes);
            var ext = isDir ? "" : Path.GetExtension(name);

            rows.Add(("Name", name));
            rows.Add(("Type", type));
            if (!string.IsNullOrEmpty(ext))  rows.Add(("Ext",      ext.ToLower()));
            if (!isDir)
            {
                rows.Add(("Size", item.SubItems.Count > 1 ? item.SubItems[1].Text : "—"));
                if (bytes > 0) rows.Add(("Bytes", $"{bytes:N0}"));
            }
            rows.Add(("", ""));
            if (!string.IsNullOrEmpty(mod)) rows.Add(("Modified", mod));
            rows.Add(("Folder",    _currentPath));
            rows.Add(("Full path", Path.Combine(_currentPath, name)));
        }

        // Render rows
        int y   = 4;
        int pw  = Math.Max(infoPanel.ClientSize.Width - infoPanel.Padding.Horizontal, 20);
        const int KEY_W  = 68;
        const int MARGIN = 4;
        const int ROW_H  = 19;

        foreach (var (key, val) in rows)
        {
            if (key.Length == 0)
            {
                var sep = new Panel
                {
                    Left      = MARGIN,
                    Top       = y + 3,
                    Width     = pw - MARGIN * 2,
                    Height    = 1,
                    BackColor = SystemColors.ControlDark,
                };
                infoPanel.Controls.Add(sep);
                y += 9;
                continue;
            }

            var kl = new Label
            {
                Left      = MARGIN,
                Top       = y,
                Width     = KEY_W,
                Height    = ROW_H,
                Text      = key + ":",
                TextAlign = ContentAlignment.TopLeft,
            };
            var vl = new Label
            {
                Left         = MARGIN + KEY_W + 2,
                Top          = y,
                Width        = pw - MARGIN - KEY_W - 4,
                Height       = ROW_H,
                Text         = val,
                AutoEllipsis = true,
            };
            infoPanel.Controls.Add(kl);
            infoPanel.Controls.Add(vl);
            y += ROW_H + 1;
        }

        infoPanel.ResumeLayout();
    }

    private static string Str(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v)) return "";
        return v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString();
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)       return $"{bytes} B";
        if (bytes < 1048576)    return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1073741824) return $"{bytes / 1048576.0:F1} MB";
        return $"{bytes / 1073741824.0:F1} GB";
    }

    private static string GetFileType(string name)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return ext switch
        {
            ".exe" or ".dll"          => "Application",
            ".txt"                    => "Text File",
            ".pdf"                    => "PDF Document",
            ".zip" or ".rar" or ".7z" => "Archive",
            ".enc"                    => "Encrypted File",
            ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" => "Image",
            ".mp3" or ".wav" or ".flac" => "Audio",
            ".mp4" or ".avi" or ".mkv"  => "Video",
            ".doc" or ".docx"         => "Word Document",
            ".xls" or ".xlsx"         => "Excel Document",
            ".bat" or ".cmd"          => "Batch Script",
            ".ps1"                    => "PowerShell Script",
            _ => string.IsNullOrEmpty(ext) ? "File" : ext.TrimStart('.').ToUpper() + " File",
        };
    }

    private static ToolStripButton MakeBtn(string text, string tip, string? iconFile = null)
    {
        var btn = new ToolStripButton
        {
            ToolTipText  = tip,
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ImageScaling = ToolStripItemImageScaling.SizeToFit,
        };
        if (iconFile != null)
        {
            var img = IconLoader.Load(iconFile);
            if (img is not null) { btn.Image = img; return btn; }
        }
        btn.Text = text; btn.DisplayStyle = ToolStripItemDisplayStyle.Text;
        return btn;
    }

    private static ToolStripMenuItem MakeMenuItem(string text, string iconFile, EventHandler handler)
    {
        var item = new ToolStripMenuItem(text) { Image = IconLoader.Load(iconFile) };
        item.Click += handler;
        return item;
    }

    private static string JsonString(string s)
        => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string? Prompt(string title, string label, string defaultValue = "")
    {
        using var dlg = new Form
        {
            Text = title, ClientSize = new Size(320, 90),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false,
        };
        var lbl = new Label { Text = label, Left = 10, Top = 10, Width = 300, AutoSize = true };
        var txt = new TextBox { Text = defaultValue, Left = 10, Top = 30, Width = 300 };
        var ok  = new Button { Text = "OK",     Left = 150, Top = 56, Width = 75, DialogResult = DialogResult.OK };
        var can = new Button { Text = "Cancel", Left = 235, Top = 56, Width = 75, DialogResult = DialogResult.Cancel };
        dlg.Controls.AddRange(new Control[] { lbl, txt, ok, can });
        dlg.AcceptButton = ok; dlg.CancelButton = can;
        txt.SelectAll();
        return dlg.ShowDialog() == DialogResult.OK ? txt.Text : null;
    }
}
