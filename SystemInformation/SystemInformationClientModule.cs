using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace mullvad.Module.SystemInformation
{
    public sealed class SystemInformationClientModule
    {
        public static string ModuleId => "mullvad.sysinfo";

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "collect": return Collect();
                    case "quick":   return Quick();
                    default:        return Err("Unknown action: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ── Quick poll — lightweight metrics for the operator's details pane ──
        // Returns: ram_used, ram_total (bytes), ram_pct, cpu_pct, idle_ms, window
        private static ulong _prevIdle, _prevKernel, _prevUser;
        private static readonly object _cpuLock = new object();

        private static string Quick()
        {
            // Memory
            ulong totalRam = 0, availRam = 0, memLoad = 0;
            try
            {
                var ms = new MEMORYSTATUSEX();
                ms.dwLength = (uint)Marshal.SizeOf(ms);
                if (GlobalMemoryStatusEx(ref ms))
                {
                    totalRam = ms.ullTotalPhys;
                    availRam = ms.ullAvailPhys;
                    memLoad  = ms.dwMemoryLoad;
                }
            }
            catch { }
            ulong usedRam = totalRam > availRam ? totalRam - availRam : 0;

            // CPU % — GetSystemTimes deltas vs the previous quick call.
            int cpuPct = -1;
            try
            {
                if (GetSystemTimes(out var idleFt, out var kernelFt, out var userFt))
                {
                    ulong idle   = ((ulong)idleFt.dwHighDateTime   << 32) | (uint)idleFt.dwLowDateTime;
                    ulong kernel = ((ulong)kernelFt.dwHighDateTime << 32) | (uint)kernelFt.dwLowDateTime;
                    ulong user   = ((ulong)userFt.dwHighDateTime   << 32) | (uint)userFt.dwLowDateTime;

                    lock (_cpuLock)
                    {
                        if (_prevKernel != 0 || _prevUser != 0)
                        {
                            ulong idleDelta   = idle   >= _prevIdle   ? idle   - _prevIdle   : 0;
                            ulong kernelDelta = kernel >= _prevKernel ? kernel - _prevKernel : 0;
                            ulong userDelta   = user   >= _prevUser   ? user   - _prevUser   : 0;
                            ulong total       = kernelDelta + userDelta;
                            if (total > 0)
                            {
                                ulong busy = total - Math.Min(idleDelta, total);
                                cpuPct = (int)((busy * 100UL) / total);
                                if (cpuPct < 0) cpuPct = 0;
                                if (cpuPct > 100) cpuPct = 100;
                            }
                        }
                        _prevIdle = idle; _prevKernel = kernel; _prevUser = user;
                    }
                }
            }
            catch { }

            // Fall back to Windows' own memory-load number if CPU sample isn't ready yet.
            if (cpuPct < 0) cpuPct = 0;

            // Idle time (ms since last user input)
            long idleMs = -1;
            try
            {
                var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
                if (GetLastInputInfo(ref lii))
                {
                    uint tick = (uint)Environment.TickCount;
                    idleMs = tick >= lii.dwTime ? tick - lii.dwTime : 0;
                }
            }
            catch { }

            // Foreground window title
            string window = "";
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd != IntPtr.Zero)
                {
                    var sb = new StringBuilder(512);
                    if (GetWindowText(hwnd, sb, sb.Capacity) > 0)
                        window = sb.ToString();
                }
            }
            catch { }

            var jb = new StringBuilder(256);
            jb.Append("{\"ram_used\":").Append(usedRam)
              .Append(",\"ram_total\":").Append(totalRam)
              .Append(",\"ram_pct\":").Append(memLoad)
              .Append(",\"cpu_pct\":").Append(cpuPct)
              .Append(",\"idle_ms\":").Append(idleMs)
              .Append(",\"window\":").Append(Json(window))
              .Append('}');
            return jb.ToString();
        }

        private static string Collect()
        {
            var sb = new StringBuilder("[");
            bool first = true;

            void Add(string category, string item, string value)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"category\":").Append(Json(category))
                  .Append(",\"item\":").Append(Json(item))
                  .Append(",\"value\":").Append(Json(value))
                  .Append("}");
            }

            // ── OS ──────────────────────────────────────────────────────────
            var osVer = Environment.OSVersion;
            Add("OS", "Platform",      osVer.Platform.ToString());
            Add("OS", "Version",       osVer.Version.ToString());
            Add("OS", "Service Pack",  osVer.ServicePack);
            Add("OS", "Architecture",  Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit");

            var osName = RegStr(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName") ?? osVer.ToString();
            var osBuild = RegStr(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuildNumber") ?? osVer.Version.Build.ToString();
            var osDisp  = RegStr(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion") ?? "";
            Add("OS", "OS Name",       osName);
            Add("OS", "Build Number",  osBuild);
            Add("OS", "Display Version", osDisp);

            // ── Machine ─────────────────────────────────────────────────────
            Add("Machine", "Computer Name", Environment.MachineName);
            Add("Machine", "User Name",     Environment.UserDomainName + "\\" + Environment.UserName);
            Add("Machine", "Processor Count", Environment.ProcessorCount.ToString());

            var sysManuf  = RegStr(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemManufacturer") ?? "N/A";
            var sysModel  = RegStr(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemProductName")  ?? "N/A";
            Add("Machine", "System Manufacturer", sysManuf);
            Add("Machine", "System Model",        sysModel);

            // ── BIOS ─────────────────────────────────────────────────────────
            var biosVer  = RegStr(@"HARDWARE\DESCRIPTION\System\BIOS", "BIOSVersion")     ?? "N/A";
            var biosDate = RegStr(@"HARDWARE\DESCRIPTION\System\BIOS", "BIOSReleaseDate") ?? "N/A";
            Add("BIOS", "BIOS Version",      biosVer);
            Add("BIOS", "BIOS Release Date", biosDate);

            // ── CPU ──────────────────────────────────────────────────────────
            var cpuName  = RegStr(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString") ?? "N/A";
            var cpuMhz   = RegStr(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "~MHz") ?? "N/A";
            Add("CPU", "Processor Name",    cpuName.Trim());
            Add("CPU", "Clock Speed (MHz)", cpuMhz);

            // ── Memory ───────────────────────────────────────────────────────
            ulong totalRam = 0, availRam = 0;
            try
            {
                var ms = new MEMORYSTATUSEX();
                ms.dwLength = (uint)Marshal.SizeOf(ms);
                GlobalMemoryStatusEx(ref ms);
                totalRam = ms.ullTotalPhys;
                availRam = ms.ullAvailPhys;
            }
            catch { }
            Add("Memory", "Total Physical Memory", FormatBytes(totalRam));
            Add("Memory", "Available Physical Memory", FormatBytes(availRam));

            // ── System dirs ──────────────────────────────────────────────────
            Add("Paths", "Windows Directory", Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            Add("Paths", "System Directory",  Environment.SystemDirectory);

            // ── Uptime ───────────────────────────────────────────────────────
            try
            {
                var ticks = (long)(uint)Environment.TickCount; // wraps at ~49 days on net472
                var up    = TimeSpan.FromMilliseconds(ticks < 0 ? (long)(uint)ticks : ticks);
                Add("System", "Uptime", $"{(int)up.TotalDays}d {up.Hours}h {up.Minutes}m");
            }
            catch { Add("System", "Uptime", "N/A"); }

            sb.Append("]");
            return sb.ToString();
        }

        // ── Registry helper ──────────────────────────────────────────────────
        private static string? RegStr(string keyPath, string valueName)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyPath, false);
                return key?.GetValue(valueName)?.ToString();
            }
            catch { return null; }
        }

        private static string FormatBytes(ulong bytes)
        {
            if (bytes == 0) return "N/A";
            if (bytes < 1073741824UL) return $"{bytes / 1048576.0:F1} MB";
            return $"{bytes / 1073741824.0:F2} GB";
        }

        private static string Err(string msg)
            => "{\"success\":false,\"error\":\"" + msg.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ") + "\"}";

        private static string Json(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                           .Replace("\r", "\\r").Replace("\n", "\\n")
                           .Replace("\t", "\\t") + "\"";
        }

        // ── P/Invoke ─────────────────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint  dwLength;
            public uint  dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME { public uint dwLowDateTime; public uint dwHighDateTime; }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    }
}
