using mullvad.Models;
using mullvad.Theme;

namespace mullvad.Forms
{
    public sealed class NetworkMapForm : Form
    {
        private readonly Func<IReadOnlyList<ClientInfo>> _getConnections;
        private readonly Func<string, Image?>            _getFlag;
        private readonly Func<string, string>            _getCountryName;

        private readonly TreeView                    _tree;
        private readonly ListView                    _list;
        private readonly FlowLayoutPanel             _barsFlow;
        private readonly ToolStripStatusLabel        _statusLabel;
        private readonly System.Windows.Forms.Timer  _timer;
        private readonly ImageList                   _imgList;

        private IReadOnlyList<ClientInfo> _snapshot = [];

        public NetworkMapForm(
            Func<IReadOnlyList<ClientInfo>> getConnections,
            Func<string, Image?>            getFlag,
            Func<string, string>            getCountryName)
        {
            _getConnections = getConnections;
            _getFlag        = getFlag;
            _getCountryName = getCountryName;

            Text            = "Network Topology";
            Size            = new Size(720, 500);
            MinimumSize     = new Size(560, 400);
            StartPosition   = FormStartPosition.CenterParent;
            Font            = new Font("Segoe UI", 9f);

            // ── Image list for tree flags ─────────────────────────────────────
            _imgList = new ImageList { ImageSize = new Size(18, 12), ColorDepth = ColorDepth.Depth32Bit };
            _imgList.Images.Add("__host", SystemIcons.Application.ToBitmap());

            // ── Toolbar ───────────────────────────────────────────────────────
            var strip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
            var btnRefresh = new ToolStripButton("⟳  Refresh") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            btnRefresh.Click += (_, _) => Rebuild();
            strip.Items.Add(btnRefresh);
            strip.Items.Add(new ToolStripSeparator());
            strip.Items.Add(new ToolStripLabel("Auto-refresh every 5 s") { ForeColor = SystemColors.GrayText });

            // ── Tree ──────────────────────────────────────────────────────────
            _tree = new TreeView
            {
                Dock              = DockStyle.Fill,
                ImageList         = _imgList,
                HideSelection     = false,
                ShowLines         = true,
                ShowRootLines     = true,
                ShowPlusMinus     = true,
                FullRowSelect     = true,
                ItemHeight        = 20,
            };
            _tree.AfterSelect += Tree_AfterSelect;

            var treePanel = new Panel { Dock = DockStyle.Left, Width = 210 };
            var treeTitleBar = new Panel { Dock = DockStyle.Top, Height = 22, Padding = new Padding(4, 3, 0, 0) };
            var treeTitleLbl = new Label
            {
                Text      = "Topology",
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            };
            treeTitleBar.Controls.Add(treeTitleLbl);
            treePanel.Controls.Add(_tree);
            treePanel.Controls.Add(treeTitleBar);

            // ── Connection list ───────────────────────────────────────────────
            _list = new ListView
            {
                Dock          = DockStyle.Fill,
                View          = View.Details,
                FullRowSelect = true,
                GridLines     = false,
                MultiSelect   = false,
                HideSelection = false,
            };
            _list.Columns.Add("Computer",   110);
            _list.Columns.Add("User",        90);
            _list.Columns.Add("IP Address", 120);
            _list.Columns.Add("OS",          80);
            _list.Columns.Add("Uptime",      70);
            _list.Columns.Add("Group",       65);

            var listPanel = new Panel { Dock = DockStyle.Fill };
            var listTitleBar = new Panel { Dock = DockStyle.Top, Height = 22, Padding = new Padding(4, 3, 0, 0) };
            var listTitleLbl = new Label
            {
                Text     = "Connections",
                AutoSize = true,
                Font     = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            };
            listTitleBar.Controls.Add(listTitleLbl);
            listPanel.Controls.Add(_list);
            listPanel.Controls.Add(listTitleBar);

            // ── Uptime bars ───────────────────────────────────────────────────
            _barsFlow = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents  = false,
                AutoScroll    = true,
                Padding       = new Padding(4, 2, 4, 2),
            };

            var barsTitleBar = new Panel { Dock = DockStyle.Top, Height = 22, Padding = new Padding(4, 3, 0, 0) };
            var barsTitleLbl = new Label
            {
                Text     = "Session Duration  (bar = % of first hour)",
                AutoSize = true,
                Font     = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            };
            barsTitleBar.Controls.Add(barsTitleLbl);

            var barsOuter = new Panel { Dock = DockStyle.Bottom, Height = 160 };
            barsOuter.Controls.Add(_barsFlow);
            barsOuter.Controls.Add(barsTitleBar);

            // ── Right panel ───────────────────────────────────────────────────
            var rightPanel = new Panel { Dock = DockStyle.Fill };
            rightPanel.Controls.Add(listPanel);
            rightPanel.Controls.Add(barsOuter);

            // ── Splitter ──────────────────────────────────────────────────────
            var splitter = new Splitter { Dock = DockStyle.Left, Width = 4 };

            // ── Status bar ────────────────────────────────────────────────────
            var statusBar = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel
            {
                Spring    = true,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            statusBar.Items.Add(_statusLabel);

            Controls.Add(rightPanel);
            Controls.Add(splitter);
            Controls.Add(treePanel);
            Controls.Add(strip);
            Controls.Add(statusBar);

            _timer = new System.Windows.Forms.Timer { Interval = 5000 };
            _timer.Tick += (_, _) => Rebuild();
            _timer.Start();

            Load += (_, _) => Rebuild();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Dispose();
            base.OnFormClosed(e);
        }

        // ── Rebuild ──────────────────────────────────────────────────────────

        private void Rebuild()
        {
            _snapshot = _getConnections();
            BuildTree();
            PopulateList(_snapshot);
            BuildBars(_snapshot);
            UpdateStatus(_snapshot);
        }

        private void UpdateStatus(IReadOnlyList<ClientInfo> snap)
        {
            int nc = snap.Count;
            int cc = snap.Select(c => c.Country.ToLower()).Distinct().Count();
            _statusLabel.Text =
                $"  {nc} connection{(nc != 1 ? "s" : "")}   ·   " +
                $"{cc} countr{(cc != 1 ? "ies" : "y")}";
        }

        // ── Tree ─────────────────────────────────────────────────────────────

        private void BuildTree()
        {
            _tree.BeginUpdate();
            var expanded = ExpandedKeys();

            _tree.Nodes.Clear();
            var root = new TreeNode($"HOST  ({_snapshot.Count})")
            {
                ImageKey         = "__host",
                SelectedImageKey = "__host",
                Tag              = "__root",
            };

            foreach (var g in _snapshot.GroupBy(c => c.Country.ToLower()).OrderBy(g => g.Key))
            {
                string code = g.Key;
                EnsureFlag(code);
                string imgKey = _imgList.Images.ContainsKey(code) ? code : "__host";

                var cn = new TreeNode($"{_getCountryName(code)}  ({g.Count()})")
                {
                    ImageKey         = imgKey,
                    SelectedImageKey = imgKey,
                    Tag              = $"__country:{code}",
                };

                foreach (var info in g.OrderBy(c => c.Computer))
                {
                    var leaf = new TreeNode($"{info.Computer}  —  {info.IpAddress}")
                    {
                        ImageKey         = imgKey,
                        SelectedImageKey = imgKey,
                        Tag              = info.Id,
                    };
                    cn.Nodes.Add(leaf);
                }

                root.Nodes.Add(cn);
            }

            _tree.Nodes.Add(root);
            root.Expand();

            // restore expansion state
            foreach (TreeNode cn in root.Nodes)
                if (expanded.Contains(cn.Tag?.ToString() ?? ""))
                    cn.Expand();

            _tree.EndUpdate();
        }

        private HashSet<string> ExpandedKeys()
        {
            var set = new HashSet<string>();
            if (_tree.Nodes.Count == 0) return set;
            var root = _tree.Nodes[0];
            if (root.IsExpanded) set.Add(root.Tag?.ToString() ?? "");
            foreach (TreeNode cn in root.Nodes)
                if (cn.IsExpanded) set.Add(cn.Tag?.ToString() ?? "");
            return set;
        }

        private void EnsureFlag(string code)
        {
            if (_imgList.Images.ContainsKey(code)) return;
            var img = _getFlag(code);
            if (img != null) _imgList.Images.Add(code, img);
        }

        private void Tree_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is not string tag) return;

            IEnumerable<ClientInfo> filtered = tag switch
            {
                "__root"           => _snapshot,
                var s when s.StartsWith("__country:") => _snapshot.Where(c =>
                    c.Country.Equals(s["__country:".Length..], StringComparison.OrdinalIgnoreCase)),
                var id             => _snapshot.Where(c => c.Id == id),
            };

            var list = filtered.ToList();
            PopulateList(list);
            BuildBars(list);
        }

        // ── Connection list ───────────────────────────────────────────────────

        private void PopulateList(IEnumerable<ClientInfo> items)
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var info in items.OrderBy(c => c.Country).ThenBy(c => c.Computer))
            {
                var lvi = new ListViewItem(info.Computer) { Tag = info.Id };
                lvi.SubItems.Add(info.Username);
                lvi.SubItems.Add(info.IpAddress);
                lvi.SubItems.Add($"{info.Os} {info.OsEdition}".Trim());
                lvi.SubItems.Add(info.Uptime);
                lvi.SubItems.Add(info.Group);
                _list.Items.Add(lvi);
            }
            _list.EndUpdate();
        }

        // ── Uptime bars ───────────────────────────────────────────────────────

        private void BuildBars(IEnumerable<ClientInfo> items)
        {
            _barsFlow.SuspendLayout();
            _barsFlow.Controls.DisposeChildren();
            _barsFlow.Controls.Clear();

            int flowW = Math.Max(1, _barsFlow.ClientSize.Width - 12);

            foreach (var info in items.OrderByDescending(c => c.ConnectedAt))
            {
                int secs = (int)(DateTime.UtcNow - info.ConnectedAt).TotalSeconds;
                int pct  = Math.Min(100, secs * 100 / 3600);

                var row = new TableLayoutPanel
                {
                    ColumnCount = 3,
                    RowCount    = 1,
                    Width       = flowW,
                    Height      = 20,
                    Margin      = new Padding(0, 1, 0, 1),
                    AutoSize    = false,
                };
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));

                var nameLbl = new Label
                {
                    Text      = Truncate(info.Computer, 16),
                    Dock      = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font      = new Font("Segoe UI", 8.25f),
                };

                var bar = new ProgressBar
                {
                    Dock    = DockStyle.Fill,
                    Minimum = 0,
                    Maximum = 100,
                    Value   = pct,
                    Style   = ProgressBarStyle.Continuous,
                };

                var timeLbl = new Label
                {
                    Text      = info.Uptime,
                    Dock      = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    Font      = new Font("Segoe UI", 7.5f),
                    ForeColor = SystemColors.GrayText,
                };

                row.Controls.Add(nameLbl, 0, 0);
                row.Controls.Add(bar,     1, 0);
                row.Controls.Add(timeLbl, 2, 0);
                _barsFlow.Controls.Add(row);
            }

            _barsFlow.ResumeLayout();
        }

        private static string Truncate(string s, int max)
            => s.Length <= max ? s : s[..(max - 1)] + "…";
    }

    internal static class ControlCollectionExtensions
    {
        internal static void DisposeChildren(this Control.ControlCollection col)
        {
            foreach (Control c in col) c.Dispose();
        }
    }
}
