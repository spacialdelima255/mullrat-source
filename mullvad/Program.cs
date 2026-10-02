using System.Runtime.InteropServices;
using mullvad.Forms;

namespace mullvad
{
    internal static class Program
    {
        private static readonly string LogPath = Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath
                ?? System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName)!,
            "log.txt");

        internal static void Log(string msg)
        {
            try
            {
                File.AppendAllText(LogPath,
                    $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
            }
            catch { }
        }

        // Detaches any inherited console — required when Native AOT links as a
        // WinExe but the PE subsystem flag isn't propagated by the AOT linker.
        [DllImport("kernel32.dll")] private static extern bool FreeConsole();

        [STAThread]
        static void Main()
        {
            try { File.Delete(LogPath); } catch { }
            Log("Main() start");

            try
            {
                FreeConsole();
                Log("FreeConsole OK");

                ApplicationConfiguration.Initialize();
                Log("ApplicationConfiguration.Initialize OK");

                Log("Showing StartupForm");
                using var startup = new StartupForm();
                if (startup.ShowDialog() != DialogResult.OK)
                {
                    Log("StartupForm cancelled");
                    return;
                }

                Log("Running Form1");
                Application.Run(new Form1(startup.SelectedPorts));
                Log("Application exited normally");
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: {ex.GetType().FullName}: {ex.Message}");
                Log($"STACK: {ex.StackTrace}");
                if (ex.InnerException is { } ie)
                    Log($"INNER: {ie.GetType().FullName}: {ie.Message}");
                MessageBox.Show(
                    $"Startup error:\n{ex.GetType().Name}: {ex.Message}\n\nSee log.txt for details.",
                    "mullvad", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
