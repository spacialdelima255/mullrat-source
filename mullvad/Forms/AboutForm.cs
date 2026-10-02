namespace mullvad.Forms
{
    public sealed class AboutForm : Form
    {
        public AboutForm()
        {
            Text            = "About";
            ClientSize      = new Size(340, 152);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;
            ShowInTaskbar   = false;

            KeyPreview = true;
            KeyDown += (_, e) => { if (e.KeyCode is Keys.Escape or Keys.Enter) Close(); };

            // ── Logo ──────────────────────────────────────────────────────
            var pic = new PictureBox
            {
                Location = new Point(14, 14),
                Size     = new Size(72, 72),
                SizeMode = PictureBoxSizeMode.Zoom,
            };
            try
            {
                using var stream = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("mullvad.Resources.icons.mullvad-logo.png");
                if (stream != null)
                    pic.Image = Image.FromStream(stream);
            }
            catch { }
            Controls.Add(pic);

            // ── Text ──────────────────────────────────────────────────────
            int tx = 100;

            Controls.Add(new Label
            {
                Text      = "Mullvad RAT",
                Font      = new Font(Font, FontStyle.Bold),
                AutoSize  = true,
                Location  = new Point(tx, 16),
            });

            Controls.Add(new Label
            {
                Text      = "*by larpexe",
                ForeColor = SystemColors.GrayText,
                AutoSize  = true,
                Location  = new Point(tx, 34),
            });

            Controls.Add(new Label
            {
                Text      = "Windows remote access framework.",
                AutoSize  = true,
                Location  = new Point(tx, 58),
            });

            Controls.Add(new Label
            {
                Text      = $"© {DateTime.Now.Year}  —  v1.0.0",
                ForeColor = SystemColors.GrayText,
                AutoSize  = true,
                Location  = new Point(tx, 76),
            });

            // ── Divider + OK ──────────────────────────────────────────────
            Controls.Add(new Panel
            {
                Location  = new Point(0, 112),
                Size      = new Size(340, 1),
                BackColor = SystemColors.ControlDark,
            });

            var btnOk = new Button
            {
                Text     = "OK",
                Size     = new Size(75, 25),
                Location = new Point(253, 120),
            };
            btnOk.Click += (_, _) => Close();
            Controls.Add(btnOk);
            AcceptButton = btnOk;
            CancelButton = btnOk;
        }
    }
}
