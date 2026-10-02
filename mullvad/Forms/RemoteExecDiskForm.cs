using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using mullvad.Network;
using mullvad.Theme;

namespace mullvad.Forms;

public sealed class RemoteExecDiskForm : Form
{
    private readonly List<ClientHandler> _handlers;

    // ── Controls ──────────────────────────────────────────────────────────────
    private ToolStrip        toolbar     = null!;
    private ToolStripButton  btnAdd      = null!;
    private ToolStripButton  btnRemove   = null!;
    private ToolStripButton  btnExecute  = null!;
    private ListView         lvFiles     = null!;
    private StatusStrip      statusStrip = null!;
    private ToolStripStatusLabel statusLabel = null!;

    public RemoteExecDiskForm(List<ClientHandler> handlers)
    {
        _handlers = handlers;
        Build();
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    private void Build()
    {
        SuspendLayout();

        Text          = "Remote Execute — From Disk";
        Size          = new Size(780, 480);
        MinimumSize   = new Size(600, 360);
        StartPosition = FormStartPosition.CenterScreen;
        Font          = new Font("Segoe UI", 9F);
        Icon          = SystemIcons.Application;

        // ── Toolbar ──────────────────────────────────────────────────────────
        toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(2) };

        btnAdd     = MakeBtn("Add Files",  "add.png",    "Add files to the list");
        btnRemove  = MakeBtn("Remove",     "delete.png", "Remove selected files");
        btnExecute = MakeBtn("Execute All","drive_go.png","Upload and run all listed files on the selected client(s)");

        btnAdd.Click     += BtnAdd_Click;
        btnRemove.Click  += BtnRemove_Click;
        btnExecute.Click += async (_, _) => await ExecuteAllAsync();

        toolbar.Items.AddRange(new ToolStripItem[]
        {
            btnAdd, btnRemove, new ToolStripSeparator(), btnExecute,
        });

        // ── ListView ─────────────────────────────────────────────────────────
        lvFiles = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            GridLines     = false,
            AllowDrop     = true,
            MultiSelect   = true,
        };
        lvFiles.Columns.Add("File Name",  220);
        lvFiles.Columns.Add("Size",        80);
        lvFiles.Columns.Add("Arguments",  200);
        lvFiles.Columns.Add("Status",     200);

        lvFiles.DragEnter += (_, e) =>
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        };
        lvFiles.DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
                foreach (var p in paths)
                    AddFilePath(p);
        };

        lvFiles.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete) RemoveSelected();
        };

        // Double-click to edit args
        lvFiles.DoubleClick += LvFiles_DoubleClick;

        // ── Status strip ──────────────────────────────────────────────────────
        statusStrip = new StatusStrip();
        statusLabel = new ToolStripStatusLabel("Drop files here, or use Add Files")
            { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        statusStrip.Items.Add(statusLabel);

        Controls.Add(lvFiles);
        Controls.Add(toolbar);
        Controls.Add(statusStrip);

        ResumeLayout();
    }

    private ToolStripButton MakeBtn(string text, string icon, string tip)
    {
        var btn = new ToolStripButton(text) { ToolTipText = tip };
        var img = IconLoader.Load(icon);
        if (img is not null) { btn.Image = img; btn.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText; }
        return btn;
    }

    // ── Add / Remove ──────────────────────────────────────────────────────────

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        using var ofd = new OpenFileDialog
        {
            Title      = "Select files to execute remotely",
            Filter     = "Executable / script|*.exe;*.bat;*.cmd;*.ps1;*.vbs|All files (*.*)|*.*",
            Multiselect = true,
        };
        if (ofd.ShowDialog(this) != DialogResult.OK) return;
        foreach (var p in ofd.FileNames)
            AddFilePath(p);
    }

    private void BtnRemove_Click(object? sender, EventArgs e) => RemoveSelected();

    private void RemoveSelected()
    {
        var sel = lvFiles.SelectedItems.Cast<ListViewItem>().ToList();
        foreach (var lvi in sel) lvFiles.Items.Remove(lvi);
        UpdateStatus();
    }

    private void AddFilePath(string path)
    {
        if (!File.Exists(path)) return;
        // Avoid duplicates
        foreach (ListViewItem existing in lvFiles.Items)
            if (string.Equals(existing.Tag as string, path, StringComparison.OrdinalIgnoreCase)) return;

        long bytes = new FileInfo(path).Length;
        var lvi = new ListViewItem(Path.GetFileName(path));
        lvi.SubItems.Add(FormatSize(bytes));
        lvi.SubItems.Add("");        // Arguments — editable via double-click
        lvi.SubItems.Add("Pending");
        lvi.Tag = path;
        lvFiles.Items.Add(lvi);
        UpdateStatus();
    }

    private void LvFiles_DoubleClick(object? sender, EventArgs e)
    {
        if (lvFiles.SelectedItems.Count == 0) return;
        var lvi  = lvFiles.SelectedItems[0];
        string current = lvi.SubItems[2].Text;
        string args = InputDialog.Show("Arguments (optional):", "Arguments", current);
        lvi.SubItems[2].Text = args;
    }

    // ── Execute ───────────────────────────────────────────────────────────────

    private async Task ExecuteAllAsync()
    {
        if (lvFiles.Items.Count == 0) { SetStatus("No files to execute."); return; }
        if (_handlers.Count == 0)     { SetStatus("No clients selected.");  return; }

        btnExecute.Enabled = false;
        btnAdd.Enabled     = false;
        btnRemove.Enabled  = false;

        try
        {
            int total = lvFiles.Items.Count;
            for (int i = 0; i < lvFiles.Items.Count; i++)
            {
                var lvi  = lvFiles.Items[i];
                string filePath = lvi.Tag as string ?? "";
                string args     = lvi.SubItems[2].Text;
                string name     = lvi.SubItems[0].Text;

                SetItemStatus(lvi, "Uploading…");
                SetStatus($"Executing {i + 1}/{total}: {name}");

                try
                {
                    string lastMsg = "";
                    await RemoteExecute.ExecuteFileOnClients(this, _handlers, filePath, args,
                        m => { lastMsg = m; if (!IsDisposed) BeginInvoke(() => SetStatus(m)); });

                    SetItemStatus(lvi, lastMsg.Contains("failed") ? "Failed" : "Sent");
                }
                catch (Exception ex)
                {
                    SetItemStatus(lvi, "Error: " + ex.Message);
                }
            }
            SetStatus($"Done — executed {total} file(s) on {_handlers.Count} client(s).");
        }
        finally
        {
            if (!IsDisposed)
            {
                btnExecute.Enabled = true;
                btnAdd.Enabled     = true;
                btnRemove.Enabled  = true;
            }
        }
    }

    private void SetItemStatus(ListViewItem lvi, string status)
    {
        if (IsDisposed) return;
        BeginInvoke(() =>
        {
            if (lvi.ListView is not null)
                lvi.SubItems[3].Text = status;
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void UpdateStatus() =>
        SetStatus(lvFiles.Items.Count == 0
            ? "Drop files here, or use Add Files"
            : $"{lvFiles.Items.Count} file(s) queued — double-click to edit arguments, then Execute All");

    private void SetStatus(string msg)
    {
        if (!IsDisposed) BeginInvoke(() => { if (!IsDisposed) statusLabel.Text = msg; });
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)        return bytes + " B";
        if (bytes < 1024 * 1024) return (bytes / 1024) + " KB";
        return (bytes / (1024 * 1024)) + " MB";
    }
}
