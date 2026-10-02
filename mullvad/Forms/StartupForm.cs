namespace mullvad.Forms;

public sealed class StartupForm : Form
{
    private const string ResPrefix = "mullvad.Resources.icons.";

    private NumericUpDown    nudPort    = null!;
    private Button           btnAdd     = null!;
    private ListBox          lstPorts   = null!;
    private Button           btnRemove  = null!;
    private Button           btnStart   = null!;
    private ContextMenuStrip ctxPorts   = null!;

    public IReadOnlyList<int> SelectedPorts =>
        lstPorts.Items.Cast<int>().ToList();

    public StartupForm()
    {
        Build();
    }

    private void Build()
    {
        SuspendLayout();

        Text            = "mullvad — Server Configuration";
        ClientSize      = new Size(340, 242);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize     = new Size(356, 281);
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9F);

        var iconStream = System.Reflection.Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(ResPrefix + "mullvad.ico");
        if (iconStream != null) try { Icon = new Icon(iconStream); } catch { }

        // ── Port entry row ────────────────────────────────────────────────────
        var lblPort = new Label
        {
            Text     = "Port:",
            Location = new Point(16, 19),
            AutoSize = true,
        };

        nudPort = new NumericUpDown
        {
            Location = new Point(52, 15),
            Size     = new Size(84, 23),
            Minimum  = 1,
            Maximum  = 65535,
            Value    = 7777,
            Font     = new Font("Segoe UI", 9F),
        };
        nudPort.KeyDown += (_, e) => { if (e.KeyCode == Keys.Return) AddPort(); };

        btnAdd = new Button
        {
            Text      = "Add",
            Location  = new Point(146, 14),
            Size      = new Size(54, 25),
            FlatStyle = FlatStyle.System,
        };
        btnAdd.Click += (_, _) => AddPort();

        // ── Port list ─────────────────────────────────────────────────────────
        var lblList = new Label
        {
            Text     = "Listen on:",
            Location = new Point(16, 52),
            AutoSize = true,
        };

        lstPorts = new ListBox
        {
            Location      = new Point(16, 70),
            Size          = new Size(308, 112),
            Font          = new Font("Segoe UI", 9F),
            SelectionMode = SelectionMode.One,
        };
        lstPorts.Items.Add(7777);

        // ── Right-click context menu ──────────────────────────────────────────
        ctxPorts = new ContextMenuStrip();

        var miAdd = new ToolStripMenuItem("Add port");
        { var img = Theme.IconLoader.Load("application_add.png"); if (img != null) miAdd.Image = img; }
        miAdd.Click += (_, _) => AddPort();

        var miRemove = new ToolStripMenuItem("Remove selected");
        { var img = Theme.IconLoader.Load("delete.png"); if (img != null) miRemove.Image = img; }
        miRemove.Click += (_, _) =>
        {
            if (lstPorts.SelectedIndex >= 0 && lstPorts.Items.Count > 1)
                lstPorts.Items.RemoveAt(lstPorts.SelectedIndex);
        };

        ctxPorts.Items.AddRange(new ToolStripItem[] { miAdd, new ToolStripSeparator(), miRemove });
        ctxPorts.Opening += (_, _) => miRemove.Enabled = lstPorts.SelectedIndex >= 0 && lstPorts.Items.Count > 1;
        lstPorts.ContextMenuStrip = ctxPorts;

        // ── Action row ────────────────────────────────────────────────────────
        btnRemove = new Button
        {
            Text      = "Remove",
            Location  = new Point(16, 196),
            Size      = new Size(76, 26),
            FlatStyle = FlatStyle.System,
        };
        btnRemove.Click += (_, _) =>
        {
            if (lstPorts.SelectedIndex >= 0 && lstPorts.Items.Count > 1)
                lstPorts.Items.RemoveAt(lstPorts.SelectedIndex);
        };

        btnStart = new Button
        {
            Text      = "Start Server",
            Location  = new Point(228, 196),
            Size      = new Size(96, 26),
            FlatStyle = FlatStyle.System,
        };
        btnStart.Font   = new Font("Segoe UI", 9F, FontStyle.Bold);
        btnStart.Click += (_, _) =>
        {
            if (lstPorts.Items.Count == 0) return;
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.AddRange(new Control[]
        {
            lblPort, nudPort, btnAdd,
            lblList, lstPorts,
            btnRemove, btnStart,
        });

        AcceptButton = btnStart;
        ResumeLayout(false);
        PerformLayout();
    }

    private void AddPort()
    {
        var port = (int)nudPort.Value;
        if (!lstPorts.Items.Contains(port))
            lstPorts.Items.Add(port);
        lstPorts.SelectedItem = port;
    }
}
