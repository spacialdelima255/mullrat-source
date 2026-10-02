using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace mullvad.Module.AdvancedSystemInformation
{
    public sealed class AdvancedSystemInformationClientModule
    {
        public static string ModuleId => "mullvad.advsysinfo";

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "collect": return Collect();
                    default:        return Err("Unknown action: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // Each row: {category, item, value}
        // category uses "/" to indicate sub-category, e.g. "Software Environment/Running Tasks"
        private static string Collect()
        {
            var rows = new List<(string cat, string item, string value)>();

            CollectSystemSummary(rows);
            CollectHardwareResources(rows);
            CollectComponents(rows);
            CollectSoftwareEnvironment(rows);

            var sb = new StringBuilder("[");
            bool first = true;
            foreach (var (cat, item, val) in rows)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"category\":").Append(Json(cat))
                  .Append(",\"item\":").Append(Json(item))
                  .Append(",\"value\":").Append(Json(val))
                  .Append("}");
            }
            sb.Append("]");
            return sb.ToString();
        }

        // ── System Summary ───────────────────────────────────────────────────
        private static void CollectSystemSummary(List<(string, string, string)> rows)
        {
            void Add(string item, string val) => rows.Add(("System Summary", item, val));

            var osVer    = Environment.OSVersion;
            var osName   = RegStr(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName") ?? osVer.ToString();
            var osBuild  = RegStr(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuildNumber") ?? "";
            var ubr      = RegStr(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "UBR") ?? "";
            var dispVer  = RegStr(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion") ?? "";

            Add("OS Name",              osName);
            Add("Version",              $"{osVer.Version}  Build {osBuild}" + (ubr.Length > 0 ? $".{ubr}" : ""));
            Add("Other OS Description", "Not Available");
            Add("OS Manufacturer",      "Microsoft Corporation");
            Add("System Name",          Environment.MachineName);

            var sysManuf = RegStr(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemManufacturer") ?? "N/A";
            var sysModel = RegStr(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemProductName")  ?? "N/A";
            Add("System Manufacturer", sysManuf);
            Add("System Model",        sysModel);
            Add("System Type",         Environment.Is64BitOperatingSystem ? "x64-based PC" : "x86-based PC");

            var cpuName  = RegStr(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString") ?? "N/A";
            var cpuMhz   = RegStr(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "~MHz") ?? "";
            var cpuCores = Environment.ProcessorCount;
            Add("Processor", $"{cpuName.Trim()}, {cpuMhz} Mhz, {cpuCores} Core(s), {cpuCores} Logical Processor(s)");

            var biosVer  = RegStr(@"HARDWARE\DESCRIPTION\System\BIOS", "BIOSVersion")     ?? "N/A";
            var biosDate = RegStr(@"HARDWARE\DESCRIPTION\System\BIOS", "BIOSReleaseDate") ?? "N/A";
            Add("BIOS Version/Date", $"{sysManuf} {biosVer}, {biosDate}");
            Add("SMBIOS Version", RegStr(@"HARDWARE\DESCRIPTION\System\BIOS", "SMBIOSBIOSVersion") ?? "N/A");

            Add("Windows Directory", Catch(() => Environment.GetFolderPath(Environment.SpecialFolder.Windows)));
            Add("System Directory",  Catch(() => Environment.SystemDirectory));
            Add("Boot Device",       @"\Device\HarddiskVolume1");

            Add("Locale", Catch(() => System.Globalization.CultureInfo.CurrentCulture.DisplayName));
            Add("Hardware Abstraction Layer", $"Version = \"{osVer.Version}\"");
            Add("User Name",   Catch(() => Environment.UserDomainName + "\\" + Environment.UserName));
            Add("Time Zone",   Catch(() => TimeZoneInfo.Local.StandardName));

            ulong totalPhys = 0, availPhys = 0, totalVirt = 0, availVirt = 0, pageFile = 0;
            try
            {
                var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                GlobalMemoryStatusEx(ref ms);
                totalPhys = ms.ullTotalPhys;
                availPhys = ms.ullAvailPhys;
                totalVirt = ms.ullTotalVirtual;
                availVirt = ms.ullAvailVirtual;
                pageFile  = ms.ullTotalPageFile - ms.ullTotalPhys;
            }
            catch { }

            Add("Installed Physical Memory (RAM)", FormatBytes(totalPhys));
            Add("Total Physical Memory",           FormatBytes(totalPhys));
            Add("Available Physical Memory",       FormatBytes(availPhys));
            Add("Total Virtual Memory",            FormatBytes(totalVirt));
            Add("Available Virtual Memory",        FormatBytes(availVirt));
            Add("Page File Space",                 FormatBytes(pageFile));
            Add("Page File",                       Catch(() => Environment.GetFolderPath(Environment.SpecialFolder.Windows)[0] + ":\\pagefile.sys"));
        }

        // ── Hardware Resources ───────────────────────────────────────────────
        private static void CollectHardwareResources(List<(string, string, string)> rows)
        {
            rows.Add(("Hardware Resources/Conflicts/Sharing", "Note", "See Device Manager for conflict details"));
            rows.Add(("Hardware Resources/DMA",    "Note", "DMA information requires WMI / Device Manager"));
            rows.Add(("Hardware Resources/I/O",    "Note", "I/O information requires WMI / Device Manager"));
            rows.Add(("Hardware Resources/IRQs",   "Note", "IRQ information requires WMI / Device Manager"));
            rows.Add(("Hardware Resources/Memory", "Note", "Memory map requires WMI / Device Manager"));
        }

        // ── Components ───────────────────────────────────────────────────────
        private static void CollectComponents(List<(string, string, string)> rows)
        {
            // Display
            try
            {
                using var displayKey = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", false);
                var driverDesc = displayKey?.GetValue("DriverDesc")?.ToString() ?? "N/A";
                var adapterMem = displayKey?.GetValue("HardwareInformation.MemorySize")?.ToString() ?? "N/A";
                rows.Add(("Components/Display", "Name",         driverDesc));
                rows.Add(("Components/Display", "Adapter RAM",  adapterMem));
                rows.Add(("Components/Display", "Driver",       displayKey?.GetValue("DriverVersion")?.ToString() ?? "N/A"));
            }
            catch
            {
                rows.Add(("Components/Display", "Status", "Unable to read display information"));
            }

            // Network adapters
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    rows.Add(("Components/Network", nic.Name, $"{nic.Description} — {nic.OperationalStatus}"));
                }
            }
            catch { rows.Add(("Components/Network", "Error", "Unable to enumerate network interfaces")); }

            // Storage — drives
            try
            {
                foreach (var drive in System.IO.DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    rows.Add(("Components/Storage", drive.Name,
                        $"{drive.DriveType} — {FormatBytes((ulong)drive.TotalSize)} total, {FormatBytes((ulong)drive.AvailableFreeSpace)} free"));
                }
            }
            catch { rows.Add(("Components/Storage", "Error", "Unable to enumerate drives")); }

            // USB via registry
            rows.Add(("Components/USB", "Note", "USB enumeration requires WMI / Device Manager"));
        }

        // ── Software Environment ─────────────────────────────────────────────
        private static void CollectSoftwareEnvironment(List<(string, string, string)> rows)
        {
            // Running Tasks
            try
            {
                var procs = Process.GetProcesses();
                Array.Sort(procs, (a, b) => string.Compare(a.ProcessName, b.ProcessName, StringComparison.OrdinalIgnoreCase));
                foreach (var p in procs)
                {
                    try
                    {
                        string path = "N/A";
                        try { path = p.MainModule?.FileName ?? "N/A"; } catch { }
                        rows.Add(("Software Environment/Running Tasks", p.ProcessName, $"PID: {p.Id}  Path: {path}"));
                    }
                    catch { }
                }
            }
            catch { rows.Add(("Software Environment/Running Tasks", "Error", "Unable to enumerate processes")); }

            // Startup Programs
            CollectStartup(rows, Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM");
            CollectStartup(rows, Registry.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKCU");

            // Services (basic from registry)
            try
            {
                using var svcs = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services", false);
                if (svcs != null)
                {
                    foreach (var name in svcs.GetSubKeyNames())
                    {
                        try
                        {
                            using var svc = svcs.OpenSubKey(name, false);
                            var start = svc?.GetValue("Start")?.ToString() ?? "N/A";
                            var type  = svc?.GetValue("Type")?.ToString()  ?? "N/A";
                            var img   = svc?.GetValue("ImagePath")?.ToString() ?? "N/A";
                            rows.Add(("Software Environment/Services", name, $"Start={start} Type={type} Image={img}"));
                        }
                        catch { }
                    }
                }
            }
            catch { rows.Add(("Software Environment/Services", "Error", "Unable to enumerate services")); }
        }

        private static void CollectStartup(List<(string, string, string)> rows, RegistryKey hive, string path, string hiveLabel)
        {
            try
            {
                using var key = hive.OpenSubKey(path, false);
                if (key == null) return;
                foreach (var name in key.GetValueNames())
                {
                    try { rows.Add(("Software Environment/Startup Programs", $"[{hiveLabel}] {name}", key.GetValue(name)?.ToString() ?? "")); }
                    catch { }
                }
            }
            catch { }
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static string RegStr(string keyPath, string valueName)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyPath, false);
                return key?.GetValue(valueName)?.ToString() ?? "";
            }
            catch { return ""; }
        }

        private static string Catch(Func<string> f)
        {
            try { return f(); } catch { return "N/A"; }
        }

        private static string FormatBytes(ulong bytes)
        {
            if (bytes == 0) return "N/A";
            if (bytes < 1073741824UL) return $"{bytes / 1048576.0:F1} MB";
            if (bytes < 1099511627776UL) return $"{bytes / 1073741824.0:F2} GB";
            return $"{bytes / 1099511627776.0:F2} TB";
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
    }
}
