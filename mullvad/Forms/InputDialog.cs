namespace mullvad.Forms
{
    internal static class InputDialog
    {
        internal static string Show(string prompt, string title, string defaultValue = "")
        {
            using var frm = new Form
            {
                Text            = title,
                Size            = new System.Drawing.Size(440, 140),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox     = false,
                MinimizeBox     = false,
                StartPosition   = FormStartPosition.CenterParent,
                Font            = new System.Drawing.Font("Segoe UI", 9f),
            };

            var lbl = new Label  { Text = prompt, Left = 10, Top = 12, Width = 400, Height = 20 };
            var txt = new TextBox{ Left = 10, Top = 36, Width = 400, Text = defaultValue };
            var ok  = new Button { Text = "OK",     Left = 240, Top = 68, Width = 80, DialogResult = DialogResult.OK };
            var can = new Button { Text = "Cancel", Left = 330, Top = 68, Width = 80, DialogResult = DialogResult.Cancel };

            frm.Controls.AddRange(new Control[] { lbl, txt, ok, can });
            frm.AcceptButton = ok;
            frm.CancelButton = can;

            return frm.ShowDialog() == DialogResult.OK ? txt.Text : "";
        }
    }
}
