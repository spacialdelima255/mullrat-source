using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using mullvad.Theme;

namespace mullvad.Forms
{
    internal static class ToastNotification
    {
        private static NotifyIcon? _notifyIcon;

        private static void EnsureIcon()
        {
            if (_notifyIcon != null) return;

            _notifyIcon = new NotifyIcon { Visible = true };

            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("mullvad.Resources.icons.mullvad-vpn.ico");
            if (stream != null)
                try { _notifyIcon.Icon = new Icon(stream); } catch { }

            _notifyIcon.Text = "Mullvad";
        }

        public static void Show(string title, string message)
        {
            var ownerForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            if (ownerForm != null && ownerForm.InvokeRequired)
            {
                ownerForm.BeginInvoke(() => Show(title, message));
                return;
            }

            EnsureIcon();
            _notifyIcon!.ShowBalloonTip(5000, title, message, ToolTipIcon.Info);
        }
    }
}
