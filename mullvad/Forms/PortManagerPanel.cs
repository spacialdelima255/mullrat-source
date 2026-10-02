using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms
{
    internal sealed class PortManagerPanel : Form
    {
        private readonly List<TcpServer>           _servers;
        private readonly Func<int, string?>        _addPort;
        private readonly Action<TcpServer>         _removePort;

        // ── Controls ──────────────────────────────────────────────────────────
        private ToolStrip            _toolbar    = null!;
        private ToolStripButton      _btnRemove  = null!;
        private ToolStripButton      _btnRefresh = null!;
        private ListView             _lst        = null!;
        private Panel                _addBar     = null!;
        private NumericUpDown        _nudPort    = null!;
        private Button               _btnAdd     = null!;
        private StatusStrip          _status     = null!;
        private ToolStripStatusLabel _lblStatus  = null!;

        public PortManagerPanel(
            List<TcpServer>   servers,
            Func<int, string?> addPort,
            Action<TcpServer>  removePort)
        {
            _servers    = servers;
            _addPort    = addPort;
            _removePort = removePort;
            Build();
        }

        private void Build()
        {
            SuspendLayout();

            Text      = "Port Manager";
            BackColor = ThemeManager.ControlColor;
            ForeColor = ThemeManager.ForeColor;

            // ── Toolbar ───────────────────────────────────────────────────────
            _toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };

            _btnRemove = new ToolStripButton("Remove")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText  = "Stop and remove selected port",
                Enabled      = false,
            };
            _btnRemove.Click += (_, _) => RemoveSelected();
            { var img = IconLoader.Load("delete.png"); if (img != null) { _btnRemove.Image = img; _btnRemove.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText; } }

            _btnRefresh = new ToolStripButton("Refresh")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText  = "Refresh port list",
            };
            _btnRefresh.Click += (_, _) => RefreshList();
            { var img = IconLoader.Load("refresh.png"); if (img != null) { _btnRefresh.Image = img; _btnRefresh.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText; } }

            _toolbar.Items.AddRange(new ToolStripItem[]
            {
                _btnRemove,
                new ToolStripSeparator(),
                _btnRefresh,
            });

            // ── Port list ─────────────────────────────────────────────────────
            _lst = new ListView
            {
                Dock          = DockStyle.Fill,
                View          = View.Details,
                FullRowSelect = true,
                GridLines     = false,
                MultiSelect   = false,
                Font          = new Font("Segoe UI", 9F),
                UseCompatibleStateImageBehavior = false,
            };
            _lst.Columns.Add("Port",    80);
            _lst.Columns.Add("Status", 100);
            _lst.Columns.Add("Clients",  80);
            _lst.OwnerDraw = true;
            _lst.DrawColumnHeader += (s, e) => e.DrawDefault = true;
            _lst.DrawItem         += (s, e) => e.DrawDefault = false;
            _lst.DrawSubItem      += DrawSubItem;
            _lst.SelectedIndexChanged += (_, _) => _btnRemove.Enabled = _lst.SelectedItems.Count > 0;

            // ── Add-port bar (bottom) ──────────────────────────────────────────
            _addBar = new Panel
            {
                Dock    = DockStyle.Bottom,
                Height  = 38,
                Padding = new Padding(6, 6, 6, 6),
            };

            var lblPort = new Label
            {
                Text      = "Port:",
                AutoSize  = true,
                Location  = new Point(6, 11),
                Font      = new Font("Segoe UI", 9F),
            };

            _nudPort = new NumericUpDown
            {
                Minimum  = 1,
                Maximum  = 65535,
                Value    = 7777,
                Width    = 80,
                Location = new Point(42, 8),
                Font     = new Font("Segoe UI", 9F),
            };
            _nudPort.KeyDown += (_, e) => { if (e.KeyCode == Keys.Return) AddPort(); };

            _btnAdd = new Button
            {
                Text      = "Start Listener",
                Location  = new Point(130, 7),
                Size      = new Size(110, 24),
                FlatStyle = FlatStyle.System,
                Font      = new Font("Segoe UI", 9F),
            };
            _btnAdd.Click += (_, _) => AddPort();

            _addBar.Controls.AddRange(new Control[] { lblPort, _nudPort, _btnAdd });

            // ── Status strip ──────────────────────────────────────────────────
            _status    = new StatusStrip();
            _lblStatus = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _status.Items.Add(_lblStatus);

            Controls.Add(_lst);
            Controls.Add(_addBar);
            Controls.Add(_toolbar);
            Controls.Add(_status);

            Load += (_, _) => RefreshList();

            ResumeLayout(false);
        }

        // ── Refresh ───────────────────────────────────────────────────────────
        public new void Refresh() => RefreshList();

        private void RefreshList()
        {
            _lst.BeginUpdate();
            _lst.Items.Clear();

            foreach (var server in _servers)
            {
                var item = new ListViewItem(server.Port.ToString());
                item.SubItems.Add(server.IsRunning ? "Listening" : "Stopped");
                item.SubItems.Add("—");
                item.Tag      = server;
                item.ForeColor = server.IsRunning
                    ? Color.FromArgb(30, 160, 80)
                    : Color.FromArgb(180, 60, 60);
                _lst.Items.Add(item);
            }

            _lst.EndUpdate();
            _lblStatus.Text = _servers.Count == 0
                ? "No listening ports"
                : $"{_servers.Count(s => s.IsRunning)} active, {_servers.Count(s => !s.IsRunning)} stopped";
            _btnRemove.Enabled = false;
        }

        // ── Add port ──────────────────────────────────────────────────────────
        private void AddPort()
        {
            int port = (int)_nudPort.Value;
            string? err = _addPort(port);
            if (err != null)
            {
                _lblStatus.Text = $"Error: {err}";
                return;
            }
            _lblStatus.Text = $"Port {port} started.";
            RefreshList();
        }

        // ── Remove selected ───────────────────────────────────────────────────
        private void RemoveSelected()
        {
            if (_lst.SelectedItems.Count == 0) return;
            if (_lst.SelectedItems[0].Tag is not TcpServer server) return;

            if (server.IsRunning)
            {
                var r = MessageBox.Show(
                    $"Stop listening on port {server.Port}?\nAny connected clients will be disconnected.",
                    "Remove Port", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
            }

            _removePort(server);
            _lblStatus.Text = $"Port {server.Port} removed.";
            RefreshList();
        }

        // ── Owner-draw rows ───────────────────────────────────────────────────
        private static void DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            var lv   = (ListView)sender!;
            bool sel = e.Item!.Selected && lv.Focused;
            var  bg  = sel ? SystemColors.Highlight : lv.BackColor;
            var  fg  = sel ? SystemColors.HighlightText : e.Item.ForeColor;

            using var bgBrush = new SolidBrush(bg);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

            var tf = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Trimming      = StringTrimming.EllipsisCharacter,
            };
            using var fgBrush = new SolidBrush(fg);
            var rect = e.Bounds;
            rect.X += 4;
            e.Graphics.DrawString(e.SubItem!.Text, lv.Font, fgBrush, rect, tf);
        }
    }
}
