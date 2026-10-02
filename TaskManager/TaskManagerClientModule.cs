using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace mullvad.Module.TaskManager
{
    public sealed class TaskManagerClientModule
    {
        public static string ModuleId => "mullvad.taskmanager";

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "list":    return ListProcesses();
                    case "kill":    return KillProcess(payload);
                    case "details": return GetDetails(payload);
                    default:        return Err("Unknown action: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ── list ─────────────────────────────────────────────────────────────

        private struct ProcessEntry
        {
            public string Name, Path, Desc;
            public int    Pid, ParentPid, Threads, Handles, Session;
            public long   Memory, CpuMs;
        }

        private static string ListProcesses()
        {
            // Get parent PIDs via CreateToolhelp32Snapshot
            var parentPids = GetParentPids();

            // Phase 1: collect start times for all processes so we can validate parent-child
            // relationships and detect PID reuse (parent started AFTER child → invalid parent).
            var procs      = Process.GetProcesses();
            var startTimes = new Dictionary<int, long>(procs.Length); // PID → FileTimeUtc
            foreach (var p in procs)
                try { startTimes[p.Id] = p.StartTime.ToFileTimeUtc(); } catch { }

            // Phase 2: build entries
            var entries = new List<ProcessEntry>(procs.Length);
            foreach (var p in procs)
            {
                try
                {
                    var e = new ProcessEntry { Name = p.ProcessName, Pid = p.Id };

                    // Validate parent PID: parent must exist AND have started before this process.
                    if (parentPids.TryGetValue(p.Id, out int rawParent) && rawParent != 0 &&
                        startTimes.TryGetValue(rawParent, out long parentTime) &&
                        startTimes.TryGetValue(p.Id, out long myTime) &&
                        parentTime > 0 && myTime > 0 && parentTime <= myTime)
                    {
                        e.ParentPid = rawParent;
                    }

                    try { e.Memory  = p.WorkingSet64; }                              catch { }
                    try { e.Threads = p.Threads.Count; }                              catch { }
                    try { e.Handles = p.HandleCount; }                                catch { }
                    try { e.CpuMs   = (long)p.TotalProcessorTime.TotalMilliseconds; } catch { }
                    try { e.Session = p.SessionId; }                                  catch { }
                    try { e.Path    = p.MainModule?.FileName ?? ""; }                 catch { }
                    try
                    {
                        if (!string.IsNullOrEmpty(e.Path))
                            e.Desc = FileVersionInfo.GetVersionInfo(e.Path).FileDescription ?? "";
                    }
                    catch { }
                    entries.Add(e);
                }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }

            // Build per-unique-path icon dict (skip UNC/network paths — SHGetFileInfo can block)
            var icons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.Path)) continue;
                if (e.Path.StartsWith(@"\\")) continue;
                if (!icons.ContainsKey(e.Path))
                    icons[e.Path] = ExtractIconBase64(e.Path);
            }

            int selfPid = Process.GetCurrentProcess().Id;

            // JSON: {"self_pid":N,"icons":{...},"processes":[...]}
            var sb = new StringBuilder("{\"self_pid\":").Append(selfPid).Append(",\"icons\":{");
            bool fi = true;
            foreach (var kv in icons)
            {
                if (!fi) sb.Append(',');
                fi = false;
                sb.Append(Json(kv.Key)).Append(':').Append(Json(kv.Value));
            }
            sb.Append("},\"processes\":[");

            bool fp = true;
            foreach (var e in entries)
            {
                if (!fp) sb.Append(',');
                fp = false;
                sb.Append("{\"pid\":").Append(e.Pid)
                  .Append(",\"parent_pid\":").Append(e.ParentPid)
                  .Append(",\"name\":").Append(Json(e.Name))
                  .Append(",\"memory\":").Append(e.Memory)
                  .Append(",\"threads\":").Append(e.Threads)
                  .Append(",\"handles\":").Append(e.Handles)
                  .Append(",\"cpu_ms\":").Append(e.CpuMs)
                  .Append(",\"session\":").Append(e.Session)
                  .Append(",\"path\":").Append(Json(e.Path))
                  .Append(",\"description\":").Append(Json(e.Desc))
                  .Append("}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        // ── Parent PID lookup via CreateToolhelp32Snapshot ────────────────────

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct PROCESSENTRY32
        {
            public uint    dwSize;
            public uint    cntUsage;
            public uint    th32ProcessID;
            public UIntPtr th32DefaultHeapID;
            public uint    th32ModuleID;
            public uint    cntThreads;
            public uint    th32ParentProcessID;
            public int     pcPriClassBase;
            public uint    dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string  szExeFile;
        }

        private const uint TH32CS_SNAPPROCESS = 0x00000002;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private static Dictionary<int, int> GetParentPids()
        {
            var result   = new Dictionary<int, int>();
            var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snapshot == INVALID_HANDLE_VALUE) return result;
            try
            {
                var entry = new PROCESSENTRY32();
                entry.dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
                if (Process32First(snapshot, ref entry))
                {
                    do
                    {
                        result[(int)entry.th32ProcessID] = (int)entry.th32ParentProcessID;
                    }
                    while (Process32Next(snapshot, ref entry));
                }
            }
            catch { }
            finally { try { CloseHandle(snapshot); } catch { } }
            return result;
        }

        // ── Icon extraction via SHGetFileInfo ────────────────────────────────

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
                    bmp = new Bitmap(icon.ToBitmap()); // copy before DestroyIcon

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

        // ── kill ─────────────────────────────────────────────────────────────

        private static string KillProcess(string payload)
        {
            int pid = ParsePid(payload);
            if (pid <= 0) return Err("Invalid PID");
            try
            {
                var p = Process.GetProcessById(pid);
                p.Kill();
                p.Dispose();
                return "{\"success\":true}";
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ── details ──────────────────────────────────────────────────────────

        private static string GetDetails(string payload)
        {
            int pid = ParsePid(payload);
            if (pid <= 0) return Err("Invalid PID");

            Process p;
            try { p = Process.GetProcessById(pid); }
            catch (Exception ex) { return Err(ex.Message); }

            var sb = new StringBuilder("{");
            try
            {
                sb.Append("\"pid\":").Append(p.Id);
                sb.Append(",\"name\":").Append(Json(p.ProcessName));
                try { sb.Append(",\"title\":").Append(Json(p.MainWindowTitle)); }   catch { sb.Append(",\"title\":\"\""); }
                try { sb.Append(",\"memory_working_set\":").Append(p.WorkingSet64); }       catch { }
                try { sb.Append(",\"memory_private\":").Append(p.PrivateMemorySize64); }    catch { }
                try { sb.Append(",\"memory_virtual\":").Append(p.VirtualMemorySize64); }    catch { }
                try { sb.Append(",\"memory_paged\":").Append(p.PagedMemorySize64); }        catch { }
                try { sb.Append(",\"handles\":").Append(p.HandleCount); }                   catch { }
                try { sb.Append(",\"session\":").Append(p.SessionId); }                     catch { }
                try { sb.Append(",\"priority\":").Append(Json(p.PriorityClass.ToString())); } catch { }
                try { sb.Append(",\"cpu_ms\":").Append((long)p.TotalProcessorTime.TotalMilliseconds); }       catch { }
                try { sb.Append(",\"cpu_user_ms\":").Append((long)p.UserProcessorTime.TotalMilliseconds); }   catch { }
                try { sb.Append(",\"cpu_kernel_ms\":").Append((long)p.PrivilegedProcessorTime.TotalMilliseconds); } catch { }
                try { sb.Append(",\"start_time\":").Append(Json(p.StartTime.ToString("yyyy-MM-dd HH:mm:ss"))); } catch { }
                try { sb.Append(",\"responding\":").Append(p.Responding ? "true" : "false"); } catch { }

                string path = "";
                try { path = p.MainModule?.FileName ?? ""; } catch { }
                sb.Append(",\"path\":").Append(Json(path));

                string desc = "", version = "", company = "";
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        var fvi = FileVersionInfo.GetVersionInfo(path);
                        desc    = fvi.FileDescription ?? "";
                        version = fvi.FileVersion     ?? "";
                        company = fvi.CompanyName      ?? "";
                    }
                    catch { }
                }
                sb.Append(",\"description\":").Append(Json(desc));
                sb.Append(",\"version\":").Append(Json(version));
                sb.Append(",\"company\":").Append(Json(company));

                // ── Threads ───────────────────────────────────────────────
                sb.Append(",\"thread_list\":[");
                bool tf = true;
                try
                {
                    foreach (ProcessThread t in p.Threads)
                    {
                        try
                        {
                            if (!tf) sb.Append(',');
                            tf = false;
                            string waitReason = "";
                            try
                            {
                                if (t.ThreadState == System.Diagnostics.ThreadState.Wait)
                                    waitReason = t.WaitReason.ToString();
                            }
                            catch { }
                            long tCpu   = 0;
                            try { tCpu  = (long)t.TotalProcessorTime.TotalMilliseconds; } catch { }
                            long tStart = 0;
                            try { tStart = t.StartTime.ToFileTimeUtc(); } catch { }

                            sb.Append("{\"id\":").Append(t.Id)
                              .Append(",\"state\":").Append(Json(t.ThreadState.ToString()))
                              .Append(",\"wait_reason\":").Append(Json(waitReason))
                              .Append(",\"priority\":").Append(t.CurrentPriority)
                              .Append(",\"base_priority\":").Append(t.BasePriority)
                              .Append(",\"cpu_ms\":").Append(tCpu)
                              .Append(",\"start_time\":").Append(tStart)
                              .Append("}");
                        }
                        catch { }
                    }
                }
                catch { }
                sb.Append("]");

                // ── Modules ───────────────────────────────────────────────
                sb.Append(",\"module_list\":[");
                bool mf = true;
                try
                {
                    foreach (ProcessModule m in p.Modules)
                    {
                        try
                        {
                            if (!mf) sb.Append(',');
                            mf = false;
                            sb.Append("{\"name\":").Append(Json(m.ModuleName ?? ""))
                              .Append(",\"path\":").Append(Json(m.FileName ?? ""))
                              .Append(",\"base\":").Append(Json("0x" + m.BaseAddress.ToInt64().ToString("X")))
                              .Append(",\"size\":").Append(m.ModuleMemorySize)
                              .Append("}");
                        }
                        catch { }
                    }
                }
                catch { }
                sb.Append("]");
            }
            finally { try { p.Dispose(); } catch { } }

            sb.Append("}");
            return sb.ToString();
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static int ParsePid(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return 0;
            payload = payload.Trim();
            if (payload.StartsWith("{"))
            {
                int idx = payload.IndexOf("\"pid\":", StringComparison.Ordinal);
                if (idx < 0) idx = payload.IndexOf("pid:", StringComparison.OrdinalIgnoreCase);
                if (idx < 0) return 0;
                int start = payload.IndexOf(':', idx) + 1;
                while (start < payload.Length && (payload[start] == ' ' || payload[start] == '\t')) start++;
                int end = start;
                while (end < payload.Length && char.IsDigit(payload[end])) end++;
                if (end == start) return 0;
                return int.TryParse(payload.Substring(start, end - start), out int pid) ? pid : 0;
            }
            return int.TryParse(payload, out int r) ? r : 0;
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
