using mullvad.Theme;

namespace mullvad.Forms
{
    public sealed class SettingsForm : Form
    {
        private readonly ComboBox  _cmbTheme;
        private readonly CheckBox  _chkMole;

        public SettingsForm()
        {
            Text            = "Settings";
            Size            = new System.Drawing.Size(320, 190);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            StartPosition   = FormStartPosition.CenterParent;
            Font            = new System.Drawing.Font("Segoe UI", 9.5f);

            var lblTheme = new Label
            {
                Text     = "Theme",
                Left     = 16,
                Top      = 18,
                AutoSize = true,
            };

            _cmbTheme = new ComboBox
            {
                Left          = 80,
                Top           = 14,
                Width         = 160,
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _cmbTheme.Items.AddRange(new object[] { "Light", "Dark" });
            _cmbTheme.SelectedIndex = ThemeManager.Current == AppTheme.Dark ? 1 : 0;

            _chkMole = new CheckBox
            {
                Text    = "Show mole image",
                Left    = 16,
                Top     = 54,
                AutoSize = true,
                Checked  = ThemeManager.ShowMole,
            };

            var btnApply = new Button
            {
                Text   = "Apply",
                Left   = 100,
                Top    = 112,
                Width  = 80,
                Height = 28,
            };

            var btnClose = new Button
            {
                Text         = "Close",
                Left         = 192,
                Top          = 112,
                Width        = 80,
                Height       = 28,
                DialogResult = DialogResult.Cancel,
            };

            btnApply.Click += BtnApply_Click;

            Controls.Add(lblTheme);
            Controls.Add(_cmbTheme);
            Controls.Add(_chkMole);
            Controls.Add(btnApply);
            Controls.Add(btnClose);
            CancelButton = btnClose;

            ApplyCurrentTheme();
            ThemeManager.ThemeChanged += ApplyCurrentTheme;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ThemeManager.ThemeChanged -= ApplyCurrentTheme;
            base.OnFormClosed(e);
        }

        private void BtnApply_Click(object? sender, EventArgs e)
        {
            var chosen = _cmbTheme.SelectedIndex == 1 ? AppTheme.Dark : AppTheme.Light;
            if (chosen != ThemeManager.Current)
            {
                ThemeManager.Set(chosen);
                MessageBox.Show(
                    "Theme applied. Please restart the application to ensure all components update correctly.",
                    "Restart Recommended",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            ThemeManager.SetShowMole(_chkMole.Checked);
        }

        private void ApplyCurrentTheme()
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(ApplyCurrentTheme); return; }

            ThemeManager.ApplyForm(this);

            _cmbTheme.SelectedIndex = ThemeManager.Current == AppTheme.Dark ? 1 : 0;
        }
    }
}
