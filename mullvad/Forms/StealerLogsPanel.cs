using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Windows.Forms;
using mullvad.Theme;

namespace mullvad.Forms
{
    internal sealed class StealerLogsPanel : Form
    {
        // ── Controls ──────────────────────────────────────────────────────────
        private ToolStrip           _toolbar    = null!;
        private ToolStripButton     _btnClear   = null!;
        private ToolStripStatusLabel _lblStatus  = null!;
        private ListView            _lstLogs    = null!;
        private TreeView            _treeFiles  = null!;

        // ── State ─────────────────────────────────────────────────────────────
        private readonly Dictionary<ListViewItem, byte[]>  _zips      = new();
        private readonly Dictionary<ListViewItem, string>  _filePaths = new();
        private ImageList _treeIcons = null!;

        public StealerLogsPanel()
        {
            Build();
        }

        private void Build()
        {
            SuspendLayout();

            Text      = "Stealer Logs";
            BackColor = ThemeManager.ControlColor;
            ForeColor = ThemeManager.ForeColor;

            // ── Toolbar ───────────────────────────────────────────────────────
            _toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };

            _btnClear = new ToolStripButton("Clear All")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ToolTipText  = "Remove all log entries",
            };
            _btnClear.Click += (_, _) => ClearAll();

            var lblSep   = new ToolStripSeparator();
            _lblStatus   = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

            _toolbar.Items.AddRange(new ToolStripItem[] { _btnClear, lblSep, _lblStatus });
            { var img = IconLoader.Load("broom.png"); if (img != null) _btnClear.Image = img; _btnClear.DisplayStyle = img != null ? ToolStripItemDisplayStyle.ImageAndText : ToolStripItemDisplayStyle.Text; }

            // ── Split container ───────────────────────────────────────────────
            var split = new SplitContainer
            {
                Dock        = DockStyle.Fill,
                Orientation = Orientation.Vertical,
            };
            // Must set SplitterDistance after the control has a size
            Load += (_, _) => { try { split.SplitterDistance = 220; } catch { } };

            // ── Left: log list ────────────────────────────────────────────────
            _lstLogs = new ListView
            {
                Dock          = DockStyle.Fill,
                View          = View.Details,
                FullRowSelect = true,
                GridLines     = false,
                MultiSelect   = false,
                Font          = new Font("Segoe UI", 9F),
                UseCompatibleStateImageBehavior = false,
            };
            _lstLogs.Columns.Add("Computer",   90);
            _lstLogs.Columns.Add("Time",       75);
            _lstLogs.Columns.Add("File Size",  55);
            _lstLogs.OwnerDraw = true;
            _lstLogs.DrawColumnHeader += (s, e) => e.DrawDefault = true;
            _lstLogs.DrawItem         += (s, e) => { };
            _lstLogs.DrawSubItem      += DrawLogSubItem;
            _lstLogs.SelectedIndexChanged += LstLogs_SelectionChanged;

            BuildLogContextMenu();
            split.Panel1.Controls.Add(_lstLogs);

            // ── Right: file tree ──────────────────────────────────────────────
            _treeFiles = new TreeView
            {
                Dock          = DockStyle.Fill,
                HideSelection = false,
                ShowLines     = true,
                Font          = new Font("Segoe UI", 9F),
                BorderStyle   = BorderStyle.None,
            };

            _treeIcons = new ImageList
            {
                ColorDepth = ColorDepth.Depth32Bit,
                ImageSize  = new Size(16, 16),
            };
            var folderImg = IconLoader.Load("folder.png");
            var fileImg   = IconLoader.Load("page_copy.png");
            _treeIcons.Images.Add("folder", folderImg ?? SystemIcons.Shield.ToBitmap());
            _treeIcons.Images.Add("file",   fileImg   ?? SystemIcons.WinLogo.ToBitmap());
            _treeFiles.ImageList = _treeIcons;

            BuildTreeContextMenu();
            split.Panel2.Controls.Add(_treeFiles);

            // ── Status strip ──────────────────────────────────────────────────
            var status    = new StatusStrip();
            var statusLbl = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            statusLbl.Text = "No logs";
            status.Items.Add(statusLbl);

            Controls.Add(split);
            Controls.Add(_toolbar);
            Controls.Add(status);

            ResumeLayout(false);
            UpdateStatus();
        }

        // ── Public API: add a log entry ───────────────────────────────────────
        public void AddLog(string computer, byte[] zipBytes, string? filePath = null)
        {
            if (InvokeRequired) { Invoke(new Action(() => AddLog(computer, zipBytes, filePath))); return; }

            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string size = zipBytes.Length >= 1024 * 1024
                ? $"{zipBytes.Length / 1048576.0:F1} MB"
                : $"{zipBytes.Length / 1024.0:F0} KB";

            var item = new ListViewItem(computer);
            item.SubItems.Add(time);
            item.SubItems.Add(size);
            _lstLogs.Items.Add(item);
            _zips[item] = zipBytes;
            if (!string.IsNullOrEmpty(filePath))
                _filePaths[item] = filePath;

            // Select the new entry so the tree view of the log populates right away.
            item.Selected = true;
            _lstLogs.EnsureVisible(item.Index);

            UpdateStatus();
        }

        // ── Context menus ─────────────────────────────────────────────────────
        private void BuildLogContextMenu()
        {
            var ctx = new ContextMenuStrip();

            var miFolder = new ToolStripMenuItem("Open in Folder");
            { var img = IconLoader.Load("folder.png"); if (img != null) miFolder.Image = img; }
            miFolder.Click += (_, _) => OpenInFolder();

            var miFollow = new ToolStripMenuItem("Follow Client");
            { var img = IconLoader.Load("computer.png"); if (img != null) miFollow.Image = img; }
            miFollow.Click += (_, _) => FollowClient();

            var miDelete = new ToolStripMenuItem("Delete Log");
            { var img = IconLoader.Load("delete.png"); if (img != null) miDelete.Image = img; }
            miDelete.Click += (_, _) => DeleteSelected();

            ctx.Items.AddRange(new ToolStripItem[]
            {
                miFolder, miFollow,
                new ToolStripSeparator(),
                miDelete,
            });

            ctx.Opening += (_, _) =>
            {
                bool has = _lstLogs.SelectedItems.Count > 0;
                foreach (ToolStripItem it in ctx.Items) it.Enabled = has;
            };

            _lstLogs.ContextMenuStrip = ctx;
        }

        private void BuildTreeContextMenu()
        {
            var ctx    = new ContextMenuStrip();
            var miView = new ToolStripMenuItem("View File");
            { var img = IconLoader.Load("page_copy.png"); if (img != null) miView.Image = img; }
            miView.Click += (_, _) => ViewSelectedFile();

            ctx.Items.Add(miView);
            ctx.Opening += (_, _) =>
            {
                miView.Enabled = _treeFiles.SelectedNode?.Tag is string;
            };

            _treeFiles.ContextMenuStrip = ctx;
            _treeFiles.NodeMouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Right) _treeFiles.SelectedNode = e.Node;
            };
        }

        // ── Actions ───────────────────────────────────────────────────────────
        private void ClearAll()
        {
            if (_lstLogs.Items.Count == 0) return;
            if (MessageBox.Show("Remove all log entries?", "Clear All",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _lstLogs.Items.Clear();
            _zips.Clear();
            _filePaths.Clear();
            _treeFiles.Nodes.Clear();
            UpdateStatus();
        }

        private void OpenInFolder()
        {
            if (_lstLogs.SelectedItems.Count == 0) return;
            var item = _lstLogs.SelectedItems[0];
            if (_filePaths.TryGetValue(item, out string? fp) && !string.IsNullOrEmpty(fp))
            {
                string? dir = Path.GetDirectoryName(fp);
                if (dir != null && Directory.Exists(dir))
                    System.Diagnostics.Process.Start("explorer.exe", dir);
            }
        }

        private void FollowClient() { /* hook up from outside if needed */ }

        private void DeleteSelected()
        {
            if (_lstLogs.SelectedItems.Count == 0) return;
            var item = _lstLogs.SelectedItems[0];
            if (_filePaths.TryGetValue(item, out string? fp) && File.Exists(fp))
                try { File.Delete(fp); } catch { }

            _zips.Remove(item);
            _filePaths.Remove(item);
            _lstLogs.Items.Remove(item);
            _treeFiles.Nodes.Clear();
            UpdateStatus();
        }

        private void ViewSelectedFile()
        {
            var node = _treeFiles.SelectedNode;
            if (node?.Tag is not string entryPath) return;

            if (_lstLogs.SelectedItems.Count == 0) return;
            var logItem = _lstLogs.SelectedItems[0];
            if (!_zips.TryGetValue(logItem, out byte[]? zipBytes) || zipBytes == null) return;

            try
            {
                using var ms      = new MemoryStream(zipBytes);
                using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
                string    norm    = entryPath.Replace('\\', '/');
                var       entry   = archive.GetEntry(norm);
                if (entry == null) return;

                using var sr = new System.IO.StreamReader(entry.Open());
                string content = sr.ReadToEnd();

                var frm = new Form
                {
                    Text            = Path.GetFileName(entryPath),
                    ClientSize      = new Size(700, 500),
                    StartPosition   = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.SizableToolWindow,
                };
                var txt = new RichTextBox
                {
                    Dock      = DockStyle.Fill,
                    ReadOnly  = true,
                    Font      = new Font("Consolas", 9F),
                    WordWrap  = false,
                    Text      = content,
                    ScrollBars = RichTextBoxScrollBars.Both,
                };
                frm.Controls.Add(txt);
                frm.ShowDialog(this);
            }
            catch { }
        }

        // ── Log selection → populate tree ─────────────────────────────────────
        private void LstLogs_SelectionChanged(object? sender, EventArgs e)
        {
            _treeFiles.Nodes.Clear();
            if (_lstLogs.SelectedItems.Count == 0) return;

            var item = _lstLogs.SelectedItems[0];
            if (!_zips.TryGetValue(item, out byte[]? zipBytes) || zipBytes == null) return;

            try
            {
                using var ms      = new MemoryStream(zipBytes);
                using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

                foreach (var entry in archive.Entries)
                {
                    string   path  = entry.FullName.Replace('/', '\\');
                    string[] parts = path.Split('\\');
                    var      nodes = _treeFiles.Nodes;

                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (string.IsNullOrEmpty(parts[i])) continue;
                        bool isLeaf = (i == parts.Length - 1);

                        TreeNode? found = null;
                        foreach (TreeNode n in nodes)
                            if (n.Text.StartsWith(parts[i])) { found = n; break; }

                        if (found == null)
                        {
                            string label = isLeaf
                                ? parts[i] + $"  ({FormatSize(entry.Length)})"
                                : parts[i];
                            found = new TreeNode(label)
                            {
                                ImageKey         = isLeaf ? "file" : "folder",
                                SelectedImageKey = isLeaf ? "file" : "folder",
                            };
                            if (isLeaf) found.Tag = path;
                            nodes.Add(found);
                        }
                        nodes = found.Nodes;
                    }
                }
            }
            catch { }
        }

        private static string FormatSize(long bytes)
            => bytes >= 1024 ? $"{bytes / 1024:F0} KB" : $"{bytes} B";

        // ── Owner-draw log list ───────────────────────────────────────────────
        private void DrawLogSubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            var lv  = (ListView)sender!;
            bool sel = e.Item!.Selected && lv.Focused;
            var bg  = sel ? SystemColors.Highlight : lv.BackColor;
            var fg  = sel ? SystemColors.HighlightText : lv.ForeColor;

            using var bgBrush = new SolidBrush(bg);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

            if (e.ColumnIndex == 0)
            {
                int x  = e.Bounds.Left + 2;
                int cy = e.Bounds.Top + (e.Bounds.Height - 16) / 2;

                var bricksImg = IconLoader.Load("bricks.png");
                if (bricksImg != null) { e.Graphics.DrawImage(bricksImg, x, cy, 16, 16); x += 18; }

                var compImg = IconLoader.Load("computer.png");
                if (compImg != null) { e.Graphics.DrawImage(compImg, x, cy, 16, 16); x += 18; }

                TextRenderer.DrawText(e.Graphics, e.SubItem!.Text, e.Item.Font,
                    new Rectangle(x, e.Bounds.Top, e.Bounds.Right - x, e.Bounds.Height),
                    fg, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            }
            else
            {
                TextRenderer.DrawText(e.Graphics, e.SubItem!.Text, e.Item.Font,
                    new Rectangle(e.Bounds.Left + 3, e.Bounds.Top, e.Bounds.Width - 3, e.Bounds.Height),
                    fg, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            }
        }

        private void UpdateStatus()
        {
            // Update the toolbar status label
            foreach (ToolStripItem it in _toolbar.Items)
            {
                if (it is ToolStripStatusLabel lbl)
                    lbl.Text = _lstLogs.Items.Count == 0 ? "No logs" : $"{_lstLogs.Items.Count} log(s)";
            }
        }
    }
}
