// language: C#, file: ServicesClientModule.cs, target: net472
// *Windows Services management — enumerate, start, stop, restart, inspect*

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;

using Microsoft.Win32;

namespace mullvad.Module.Services
{
    public sealed class ServicesClientModule
    {
        public static string ModuleId => "mullvad.services";

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "list":    return ListServices();
                    case "start":   return ControlService(payload, "start");
                    case "stop":    return ControlService(payload, "stop");
                    case "restart": return ControlService(payload, "restart");
                    case "details": return GetDetails(payload);
                    default:        return Err("Unknown action: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  list
        // ─────────────────────────────────────────────────────────────────────

        private static string ListServices()
        {
            var controllers = ServiceController.GetServices();
            var entries     = new List<ServiceEntry>(controllers.Length);

            foreach (var sc in controllers)
            {
                try
                {
                    var e = new ServiceEntry
                    {
                        Name        = sc.ServiceName,
                        DisplayName = sc.DisplayName,
                        Status      = sc.Status.ToString(),
                    };
                    try { e.Pid = GetServicePid(sc.ServiceName); } catch { }

                    ReadRegistryInfo(sc.ServiceName, ref e);
                    entries.Add(e);
                }
                catch { }
                finally { try { sc.Dispose(); } catch { } }
            }

            entries.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

            // Build icon dict: unique executable paths → base64 icon
            var icons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.ImagePath)) continue;
                if (e.ImagePath.StartsWith(@"\\")) continue;
                if (!icons.ContainsKey(e.ImagePath))
                    icons[e.ImagePath] = ExtractIconBase64(e.ImagePath);
            }

            var sb = new StringBuilder("{\"icons\":{");
            bool fi = true;
            foreach (var kv in icons)
            {
                if (string.IsNullOrEmpty(kv.Value)) continue;
                if (!fi) sb.Append(',');
                fi = false;
                sb.Append(Json(kv.Key)).Append(':').Append(Json(kv.Value));
            }
            sb.Append("},\"services\":[");

            bool fs = true;
            foreach (var e in entries)
            {
                if (!fs) sb.Append(',');
                fs = false;
                sb.Append("{\"name\":").Append(Json(e.Name))
                  .Append(",\"display_name\":").Append(Json(e.DisplayName))
                  .Append(",\"description\":").Append(Json(e.Description))
                  .Append(",\"status\":").Append(Json(e.Status))
                  .Append(",\"start_type\":").Append(Json(e.StartType))
                  .Append(",\"log_on_as\":").Append(Json(e.LogOnAs))
                  .Append(",\"image_path\":").Append(Json(e.ImagePath))
                  .Append(",\"pid\":").Append(e.Pid)
                  .Append("}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  start / stop / restart
        // ─────────────────────────────────────────────────────────────────────

        private static string ControlService(string payload, string op)
        {
            string name = ParseName(payload);
            if (string.IsNullOrEmpty(name)) return Err("Service name required");

            using var sc = new ServiceController(name);
            try
            {
                switch (op)
                {
                    case "start":
                        if (sc.Status == ServiceControllerStatus.Running)
                            return "{\"success\":true,\"note\":\"already running\"}";
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        break;

                    case "stop":
                        if (sc.Status == ServiceControllerStatus.Stopped)
                            return "{\"success\":true,\"note\":\"already stopped\"}";
                        if (!sc.CanStop) return Err("Service cannot be stopped");
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        break;

                    case "restart":
                        if (sc.Status != ServiceControllerStatus.Stopped)
                        {
                            if (!sc.CanStop) return Err("Service cannot be stopped for restart");
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        }
                        sc.Refresh();
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        break;
                }
                sc.Refresh();
                return "{\"success\":true,\"status\":" + Json(sc.Status.ToString()) + "}";
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  details
        // ─────────────────────────────────────────────────────────────────────

        private static string GetDetails(string payload)
        {
            string name = ParseName(payload);
            if (string.IsNullOrEmpty(name)) return Err("Service name required");

            using var sc = new ServiceController(name);
            try { sc.Refresh(); }
            catch (Exception ex) { return Err(ex.Message); }

            var e = new ServiceEntry
            {
                Name        = sc.ServiceName,
                DisplayName = sc.DisplayName,
                Status      = sc.Status.ToString(),
            };
            try { e.Pid = GetServicePid(sc.ServiceName); } catch { }
            ReadRegistryInfo(sc.ServiceName, ref e);

            var sb = new StringBuilder("{");
            sb.Append("\"name\":").Append(Json(e.Name))
              .Append(",\"display_name\":").Append(Json(e.DisplayName))
              .Append(",\"description\":").Append(Json(e.Description))
              .Append(",\"status\":").Append(Json(e.Status))
              .Append(",\"start_type\":").Append(Json(e.StartType))
              .Append(",\"log_on_as\":").Append(Json(e.LogOnAs))
              .Append(",\"image_path\":").Append(Json(e.ImagePath))
              .Append(",\"pid\":").Append(e.Pid);

            // Dependencies (services this one depends on)
            sb.Append(",\"depends_on\":[");
            bool fd = true;
            try
            {
                foreach (var dep in sc.ServicesDependedOn)
                {
                    if (!fd) sb.Append(',');
                    fd = false;
                    sb.Append(Json(dep.DisplayName));
                    try { dep.Dispose(); } catch { }
                }
            }
            catch { }
            sb.Append("]");

            // Dependents (services that depend on this one)
            sb.Append(",\"depended_on_by\":[");
            bool fdb = true;
            try
            {
                foreach (var dep in sc.DependentServices)
                {
                    if (!fdb) sb.Append(',');
                    fdb = false;
                    sb.Append(Json(dep.DisplayName));
                    try { dep.Dispose(); } catch { }
                }
            }
            catch { }
            sb.Append("]");

            // Icon
            string icon64 = string.IsNullOrEmpty(e.ImagePath) ? "" : ExtractIconBase64(e.ImagePath);
            sb.Append(",\"icon\":").Append(Json(icon64));

            sb.Append("}");
            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Registry helpers
        // ─────────────────────────────────────────────────────────────────────

        private static void ReadRegistryInfo(string svcName, ref ServiceEntry e)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Services\" + svcName, writable: false);
                if (key == null) return;

                // Start type
                int startVal = (int)(key.GetValue("Start") ?? -1);
                bool delayed = false;
                try { delayed = Convert.ToInt32(key.GetValue("DelayedAutoStart") ?? 0) != 0; } catch { }

                e.StartType = startVal switch
                {
                    0 => "Boot",
                    1 => "System",
                    2 => delayed ? "Automatic (Delayed)" : "Automatic",
                    3 => "Manual",
                    4 => "Disabled",
                    _ => "Unknown",
                };

                // Log on as
                e.LogOnAs = (key.GetValue("ObjectName") as string) ?? "LocalSystem";

                // Description — may be an indirect string (@file,-id)
                string rawDesc = (key.GetValue("Description") as string) ?? "";
                e.Description = ResolveIndirectString(rawDesc);

                // Executable image path
                string imagePath = (key.GetValue("ImagePath") as string) ?? "";
                e.ImagePath = ParseExePath(imagePath);
            }
            catch { }
        }

        private static string ResolveIndirectString(string s)
        {
            if (string.IsNullOrEmpty(s) || !s.StartsWith("@")) return s;
            try
            {
                var buf = new StringBuilder(1024);
                int hr  = SHLoadIndirectString(s, buf, buf.Capacity, IntPtr.Zero);
                return hr == 0 ? buf.ToString() : s;
            }
            catch { return s; }
        }

        private static string ParseExePath(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return "";
            imagePath = imagePath.Trim();

            // Expand environment variables
            try { imagePath = Environment.ExpandEnvironmentVariables(imagePath); } catch { }

            // Quoted path: "C:\path\to\file.exe" args...
            if (imagePath.StartsWith("\""))
            {
                int end = imagePath.IndexOf('"', 1);
                return end > 0 ? imagePath.Substring(1, end - 1) : imagePath.Substring(1);
            }

            // Unquoted: C:\path\to\file.exe -args — find first .exe occurrence
            int exeIdx = imagePath.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIdx >= 0) return imagePath.Substring(0, exeIdx + 4);

            // Fallback: take everything before the first space
            int space = imagePath.IndexOf(' ');
            return space > 0 ? imagePath.Substring(0, space) : imagePath;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Get PID via QueryServiceStatusEx
        // ─────────────────────────────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_STATUS_PROCESS
        {
            public uint dwServiceType, dwCurrentState, dwControlsAccepted;
            public uint dwWin32ExitCode, dwServiceSpecificExitCode, dwCheckPoint, dwWaitHint;
            public uint dwProcessId, dwServiceFlags;
        }

        private const uint SC_MANAGER_CONNECT       = 0x0001;
        private const uint SERVICE_QUERY_STATUS      = 0x0004;
        private const uint SC_STATUS_PROCESS_INFO    = 0;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint access);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenService(IntPtr hSCManager, string serviceName, uint access);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool QueryServiceStatusEx(IntPtr hService, uint infoLevel,
            out SERVICE_STATUS_PROCESS lpBuffer, int cbBufSize, out int pcbBytesNeeded);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CloseServiceHandle(IntPtr hSCObject);

        private static int GetServicePid(string serviceName)
        {
            IntPtr scm  = IntPtr.Zero;
            IntPtr svc  = IntPtr.Zero;
            try
            {
                scm = OpenSCManager(null, null, SC_MANAGER_CONNECT);
                if (scm == IntPtr.Zero) return 0;
                svc = OpenService(scm, serviceName, SERVICE_QUERY_STATUS);
                if (svc == IntPtr.Zero) return 0;
                bool ok = QueryServiceStatusEx(svc, SC_STATUS_PROCESS_INFO,
                    out var status, Marshal.SizeOf(typeof(SERVICE_STATUS_PROCESS)), out _);
                return ok ? (int)status.dwProcessId : 0;
            }
            catch { return 0; }
            finally
            {
                if (svc  != IntPtr.Zero) try { CloseServiceHandle(svc);  } catch { }
                if (scm  != IntPtr.Zero) try { CloseServiceHandle(scm);  } catch { }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Icon extraction via SHGetFileInfo (same pattern as TaskManager module)
        // ─────────────────────────────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int    iIcon;
            public uint   dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON      = 0x100;
        private const uint SHGFI_SMALLICON = 0x001;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
            ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf,
            int cchOutBuf, IntPtr ppvReserved);

        private static string ExtractIconBase64(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "";
            var info = new SHFILEINFO();
            try
            {
                IntPtr ret = SHGetFileInfo(path, 0, ref info,
                    (uint)Marshal.SizeOf(typeof(SHFILEINFO)), SHGFI_ICON | SHGFI_SMALLICON);
                if (ret == IntPtr.Zero || info.hIcon == IntPtr.Zero) return "";

                Bitmap bmp;
                using (var icon = Icon.FromHandle(info.hIcon))
                    bmp = new Bitmap(icon.ToBitmap());
                DestroyIcon(info.hIcon);
                info.hIcon = IntPtr.Zero;

                using (bmp)
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
            catch { return ""; }
            finally
            {
                if (info.hIcon != IntPtr.Zero)
                    try { DestroyIcon(info.hIcon); } catch { }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        private struct ServiceEntry
        {
            public string Name, DisplayName, Description, Status, StartType, LogOnAs, ImagePath;
            public int    Pid;
        }

        private static string ParseName(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return "";
            payload = payload.Trim();
            if (!payload.StartsWith("{")) return payload;
            // Extract "name":"value"
            int idx = payload.IndexOf("\"name\"", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return "";
            int colon = payload.IndexOf(':', idx + 6);
            if (colon < 0) return "";
            int q1 = payload.IndexOf('"', colon + 1);
            if (q1 < 0) return "";
            int q2 = q1 + 1;
            while (q2 < payload.Length && payload[q2] != '"') q2++;
            return q2 > q1 + 1 ? payload.Substring(q1 + 1, q2 - q1 - 1) : "";
        }

        private static string Err(string msg)
            => "{\"error\":" + Json(msg) + "}";

        private static string Json(string s)
        {
            if (s == null) return "\"\"";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\r': sb.Append("\\r");  break;
                    case '\n': sb.Append("\\n");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("X4"));
                        else          sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
