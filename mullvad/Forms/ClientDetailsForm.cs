using mullvad.Models;

namespace mullvad.Forms
{
    public sealed class ClientDetailsForm : Form
    {
        public ClientDetailsForm(ClientInfo info)
        {
            Text            = $"Details — {info.Computer}";
            Size            = new System.Drawing.Size(380, 360);
            MinimumSize     = new System.Drawing.Size(320, 300);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;
            Font            = new System.Drawing.Font("Segoe UI", 9f);

            var lv = new ListView
            {
                Dock          = DockStyle.Fill,
                View          = View.Details,
                FullRowSelect = true,
                GridLines     = true,
                HeaderStyle   = ColumnHeaderStyle.None,
                UseCompatibleStateImageBehavior = false,
            };
            lv.Columns.Add("Field", 140);
            lv.Columns.Add("Value", 200);

            void Row(string k, string v) => lv.Items.Add(new ListViewItem(new[] { k, v }));

            Row("Computer",      info.Computer);
            Row("Username",      info.Username);
            Row("IP Address",    info.IpAddress);
            Row("Port",          info.Port.ToString());
            Row("OS",            info.Os);
            Row("Edition",       info.OsEdition);
            Row("Architecture",  info.Architecture);
            Row("Country",       info.Country.ToUpper());
            Row("Version",       info.Version);
            Row("Group",         info.Group);
            Row("Installed At",  info.InstalledAt);
            Row("Connected",     info.IsConnected ? "Yes" : "No");
            Row("Client ID",     info.Id);

            var btnClose = new Button
            {
                Text         = "Close",
                DialogResult = DialogResult.Cancel,
                Dock         = DockStyle.Bottom,
                Height       = 30,
            };

            Controls.Add(lv);
            Controls.Add(btnClose);
            AcceptButton = btnClose;
            CancelButton = btnClose;
        }
    }
}
