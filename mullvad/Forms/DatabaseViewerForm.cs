using System.Data;
using System.IO;
using System.Text;
using mullvad.Database;
using mullvad.Theme;

namespace mullvad.Forms;

public sealed class DatabaseViewerForm : Form
{
    private ToolStrip            toolbar     = null!;
    private ToolStripButton      btnRefresh  = null!;
    private ToolStripButton      btnExport   = null!;
    private ToolStripButton      btnClear    = null!;
    private TreeView             treeView    = null!;
    private DataGridView         grid        = null!;
    private TextBox              txtSql      = null!;
    private Button               btnExecute  = null!;
    private StatusStrip          statusStrip = null!;
    private ToolStripStatusLabel statusLabel = null!;

    public DatabaseViewerForm()
    {
        Build();
    }

    private void Build()
    {
        SuspendLayout();

        Text          = "Database";
        ClientSize    = new Size(980, 640);
        Font          = new Font("Segoe UI", 9F);
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = true;

        // ── Toolbar ──────────────────────────────────────────────────────────
        toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Top };

        btnRefresh = new ToolStripButton("Refresh") { DisplayStyle = ToolStripItemDisplayStyle.Text, ToolTipText = "Reload tree" };
        btnExport  = new ToolStripButton("Export CSV") { DisplayStyle = ToolStripItemDisplayStyle.Text, ToolTipText = "Export current view to CSV" };
        btnClear   = new ToolStripButton("Clear Table") { DisplayStyle = ToolStripItemDisplayStyle.Text, ToolTipText = "Delete all rows from current table" };

        btnRefresh.Click += (_, _) => RefreshTree();
        btnExport.Click  += BtnExport_Click;
        btnClear.Click   += BtnClear_Click;

        toolbar.Items.AddRange(new ToolStripItem[]
        {
            btnRefresh,
            new ToolStripSeparator(),
            btnExport,
            new ToolStripSeparator(),
            btnClear,
        });

        // ── Status strip ─────────────────────────────────────────────────────
        statusStrip = new StatusStrip();
        statusLabel = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        statusStrip.Items.Add(statusLabel);

        // ── SQL bar (bottom) ─────────────────────────────────────────────────
        var sqlBar = new Panel { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(3) };

        btnExecute = new Button
        {
            Text      = "Execute",
            Dock      = DockStyle.Right,
            Width     = 88,
            FlatStyle = FlatStyle.System,
        };
        btnExecute.Click += (_, _) => ExecuteSql();

        txtSql = new TextBox
        {
            Dock            = DockStyle.Fill,
            Font            = new Font("Consolas", 9F),
            PlaceholderText = "SELECT * FROM clients",
        };
        txtSql.KeyDown += (_, e) => { if (e.KeyCode == Keys.Return && e.Control) ExecuteSql(); };

        sqlBar.Controls.Add(txtSql);
        sqlBar.Controls.Add(btnExecute);

        // ── DataGridView ─────────────────────────────────────────────────────
        grid = new DataGridView
        {
            Dock                  = DockStyle.Fill,
            ReadOnly              = true,
            AllowUserToAddRows    = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect           = false,
            RowHeadersVisible     = false,
            BorderStyle           = BorderStyle.None,
            Font                  = new Font("Segoe UI", 9F),
            BackgroundColor       = SystemColors.Window,
            GridColor             = Color.FromArgb(220, 220, 220),
            CellBorderStyle       = DataGridViewCellBorderStyle.None,
            ColumnHeadersBorderStyle       = DataGridViewHeaderBorderStyle.None,
            ColumnHeadersHeightSizeMode    = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            EnableHeadersVisualStyles      = false,
        };
        grid.DefaultCellStyle.BackColor          = SystemColors.Window;
        grid.DefaultCellStyle.ForeColor          = SystemColors.WindowText;
        grid.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
        grid.DefaultCellStyle.SelectionForeColor = SystemColors.HighlightText;
        grid.DefaultCellStyle.Padding            = new Padding(2, 0, 2, 0);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 252);
        grid.ColumnHeadersDefaultCellStyle.Font        = new Font("Segoe UI", 9F, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.BackColor   = Color.FromArgb(235, 235, 240);
        grid.ColumnHeadersDefaultCellStyle.ForeColor   = SystemColors.WindowText;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(235, 235, 240);
        grid.ColumnHeadersDefaultCellStyle.Padding     = new Padding(4, 0, 4, 0);

        var rightPanel = new Panel { Dock = DockStyle.Fill };
        rightPanel.Controls.Add(grid);
        rightPanel.Controls.Add(sqlBar);

        // ── TreeView (left) ───────────────────────────────────────────────────
        var leftPanel = new Panel { Dock = DockStyle.Left, Width = 230 };

        treeView = new TreeView
        {
            Dock          = DockStyle.Fill,
            HideSelection = false,
            ShowLines     = true,
            Font          = new Font("Segoe UI", 9F),
            BorderStyle   = BorderStyle.None,
        };
        treeView.AfterSelect += OnNodeSelected;
        leftPanel.Controls.Add(treeView);

        var splitter = new Splitter { Dock = DockStyle.Left, Width = 4 };

        Controls.Add(rightPanel);
        Controls.Add(splitter);
        Controls.Add(leftPanel);
        Controls.Add(toolbar);
        Controls.Add(statusStrip);

        ResumeLayout(false);
        PerformLayout();

        Load += (_, _) => RefreshTree();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Tree building
    // ─────────────────────────────────────────────────────────────────────────
    private void RefreshTree()
    {
        var selected = treeView.SelectedNode?.Name;

        treeView.BeginUpdate();
        treeView.Nodes.Clear();

        // ── Server node ───────────────────────────────────────────────────────
        var serverNode = new TreeNode("Server") { Name = "server" };

        serverNode.Nodes.Add(MakeNode("Overview", "server.overview",
            @"SELECT 'Total Clients'        AS Metric, CAST((SELECT COUNT(*) FROM clients)      AS TEXT) AS Value
              UNION ALL SELECT 'System Info Records',   CAST((SELECT COUNT(*) FROM system_info)    AS TEXT)
              UNION ALL SELECT 'Advanced Info Records', CAST((SELECT COUNT(*) FROM advanced_info)  AS TEXT)
              UNION ALL SELECT 'Network Info Records',  CAST((SELECT COUNT(*) FROM network_info)   AS TEXT)
              UNION ALL SELECT 'Last Client Seen', COALESCE((SELECT MAX(connected_at) FROM clients), 'No data')"));

        serverNode.Nodes.Add(MakeNode("All Clients", "server.all_clients",
            "SELECT client_id, computer, username, ip, os, arch, country, connected_at FROM clients ORDER BY connected_at DESC"));

        // ── Clients ───────────────────────────────────────────────────────────
        var clients = ServerDatabase.GetAllClients();
        var clientsNode = new TreeNode($"Clients  ({clients.Count})") { Name = "clients" };

        foreach (var (clientId, computer, username, os, connectedAt) in clients)
        {
            var safeId = Esc(clientId);
            var label  = string.IsNullOrWhiteSpace(username) ? computer : $"{computer}  ({username})";

            var clientNode = new TreeNode(label) { Name = $"client.{clientId}" };
            clientNode.ToolTipText = $"ID: {clientId}\nOS: {os}\nLast seen: {connectedAt}";
            clientNode.Tag = $"SELECT computer, username, ip, os, arch, country, connected_at FROM clients WHERE client_id='{safeId}'";

            clientNode.Nodes.Add(MakeNode("System Info", $"client.{clientId}.sys",
                $"SELECT item AS Item, value AS Value, collected_at AS [Collected] FROM system_info WHERE client_id='{safeId}' ORDER BY id"));

            clientNode.Nodes.Add(MakeNode("Advanced Info", $"client.{clientId}.adv",
                $"SELECT category AS Category, item AS Item, value AS Value FROM advanced_info WHERE client_id='{safeId}' ORDER BY id"));

            clientNode.Nodes.Add(MakeNode("Network Info", $"client.{clientId}.net",
                $"SELECT adapter AS Adapter, item AS Item, value AS Value FROM network_info WHERE client_id='{safeId}' ORDER BY id"));

            clientsNode.Nodes.Add(clientNode);
        }

        treeView.Nodes.Add(serverNode);
        treeView.Nodes.Add(clientsNode);

        serverNode.Expand();
        clientsNode.Expand();

        if (selected != null)
        {
            var node = FindNode(treeView.Nodes, selected);
            if (node != null) treeView.SelectedNode = node;
        }

        treeView.EndUpdate();
        statusLabel.Text = $"{clients.Count} client(s)  •  database ready";
    }

    private static TreeNode MakeNode(string text, string name, string sql)
        => new TreeNode(text) { Name = name, Tag = sql };

    private static string Esc(string s) => s.Replace("'", "''");

    private static TreeNode? FindNode(TreeNodeCollection nodes, string name)
    {
        foreach (TreeNode n in nodes)
        {
            if (n.Name == name) return n;
            var found = FindNode(n.Nodes, name);
            if (found != null) return found;
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Node selection
    // ─────────────────────────────────────────────────────────────────────────
    private void OnNodeSelected(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is not string sql || string.IsNullOrWhiteSpace(sql)) return;
        txtSql.Text = sql.Trim();
        RunQuery(sql);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Query execution
    // ─────────────────────────────────────────────────────────────────────────
    private void ExecuteSql()
    {
        var sql = txtSql.Text.Trim();
        if (!string.IsNullOrEmpty(sql)) RunQuery(sql);
    }

    private DataTable? _currentTable;

    private void RunQuery(string sql)
    {
        try
        {
            var dt = ServerDatabase.ExecuteQuery(sql);
            _currentTable = dt;
            ShowDataTable(dt);
            statusLabel.Text = $"{dt.Rows.Count} row(s) returned";
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"SQL error: {ex.Message}";
        }
    }

    private void ShowDataTable(DataTable dt)
    {
        grid.DataSource = null;
        grid.Columns.Clear();

        grid.DataSource = dt;

        foreach (DataGridViewColumn col in grid.Columns)
        {
            col.SortMode = DataGridViewColumnSortMode.Automatic;
            col.MinimumWidth = 60;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Export / Clear
    // ─────────────────────────────────────────────────────────────────────────
    private void BtnExport_Click(object? sender, EventArgs e)
    {
        if (_currentTable == null || _currentTable.Rows.Count == 0)
        {
            statusLabel.Text = "Nothing to export.";
            return;
        }

        using var dlg = new SaveFileDialog
        {
            Title      = "Export to CSV",
            Filter     = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName   = "export.csv",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var sb = new StringBuilder();
            // Header
            for (int i = 0; i < _currentTable.Columns.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(_currentTable.Columns[i].ColumnName.Replace("\"", "\"\"")).Append('"');
            }
            sb.AppendLine();
            // Rows
            foreach (DataRow row in _currentTable.Rows)
            {
                for (int i = 0; i < _currentTable.Columns.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var val = row[i]?.ToString() ?? "";
                    sb.Append('"').Append(val.Replace("\"", "\"\"")).Append('"');
                }
                sb.AppendLine();
            }
            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
            statusLabel.Text = $"Exported {_currentTable.Rows.Count} row(s) → {dlg.FileName}";
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Export failed: {ex.Message}";
        }
    }

    private void BtnClear_Click(object? sender, EventArgs e)
    {
        var node = treeView.SelectedNode;
        if (node == null) { statusLabel.Text = "Select a table node first."; return; }

        // Derive table name from node name (e.g. "client.X.sys" → system_info)
        string? table = node.Name switch
        {
            var n when n.EndsWith(".sys") => "system_info",
            var n when n.EndsWith(".adv") => "advanced_info",
            var n when n.EndsWith(".net") => "network_info",
            "server.all_clients"          => null,
            _                             => null,
        };

        if (table == null) { statusLabel.Text = "Select a client data node to clear."; return; }

        if (MessageBox.Show($"Delete ALL rows from '{table}'?\nThis cannot be undone.",
                "Clear Table", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        try
        {
            ServerDatabase.ExecuteNonQuery($"DELETE FROM {table}");
            statusLabel.Text = $"Cleared table '{table}'.";
            if (node.Tag is string sql) RunQuery(sql);
        }
        catch (Exception ex) { statusLabel.Text = $"Error: {ex.Message}"; }
    }
}
