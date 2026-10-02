using mullvad.Theme;

namespace mullvad.Forms
{
    public class ChangelogForm : Form
    {
        public ChangelogForm()
        {
            Text            = "Changelog";
            ClientSize      = new Size(420, 340);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;
            ShowInTaskbar   = false;
            Font            = new Font("Segoe UI", 9f);

            var txt = new RichTextBox
            {
                Dock      = DockStyle.Fill,
                ReadOnly  = true,
                BorderStyle = BorderStyle.None,
                BackColor = BackColor,
                Text =
                    "v1.0.0\n" +
                    "─────────────────────────\n" +
                    "  - Initial release\n" +
                    "  - Builder with protection settings\n" +
                    "  - Anti-Debug, Anti-Tamper, Anti-VM\n" +
                    "  - Obfuscation and encryption\n" +
                    "  - Native AOT compilation\n" +
                    "  - File manager module\n" +
                    "  - Task scheduler persistence\n" +
                    "  - Profile save/load system\n",
            };

            Controls.Add(txt);
        }
    }
}
