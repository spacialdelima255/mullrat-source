using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace mullvad.Module.KeywordMonitor
{
    public sealed class KeywordMonitorModule
    {
        public static string ModuleId => "mullvad.kwmonitor";

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "scan": return Scan(payload);
                    default:     return Err("Unknown action: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        private static string Scan(string payload)
        {
            var keywords = ParseArray(payload, "keywords");
            var apps     = ParseArray(payload, "apps");

            var sb = new StringBuilder(512);
            sb.Append("{\"kw\":[");

            if (keywords.Count > 0)
            {
                var titles = GetAllWindowTitles();
                bool first = true;
                foreach (var title in titles)
                {
                    foreach (var kw in keywords)
                    {
                        if (title.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (!first) sb.Append(',');
                            first = false;
                            sb.Append("{\"k\":").Append(Json(kw))
                              .Append(",\"w\":").Append(Json(title)).Append('}');
                        }
                    }
                }
            }

            sb.Append("],\"ap\":[");

            if (apps.Count > 0)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bool first = true;
                try
                {
                    var procs = Process.GetProcesses();
                    foreach (var proc in procs)
                    {
                        try
                        {
                            string pname = proc.ProcessName;
                            string pexe  = pname + ".exe";
                            foreach (var app in apps)
                            {
                                if (string.Equals(pexe, app, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(pname, app, StringComparison.OrdinalIgnoreCase))
                                {
                                    string key = app + "|" + pexe;
                                    if (seen.Add(key))
                                    {
                                        if (!first) sb.Append(',');
                                        first = false;
                                        sb.Append("{\"a\":").Append(Json(app))
                                          .Append(",\"p\":").Append(Json(pexe)).Append('}');
                                    }
                                    break;
                                }
                            }
                        }
                        catch { }
                        finally { proc.Dispose(); }
                    }
                }
                catch { }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static List<string> GetAllWindowTitles()
        {
            var titles = new List<string>();
            EnumWindows((hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd)) return true;
                var buf = new StringBuilder(512);
                if (GetWindowText(hwnd, buf, buf.Capacity) > 0)
                {
                    string t = buf.ToString();
                    if (t.Length > 0) titles.Add(t);
                }
                return true;
            }, IntPtr.Zero);
            return titles;
        }

        private static List<string> ParseArray(string json, string key)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(json)) return result;
            string needle = "\"" + key + "\"";
            int idx = json.IndexOf(needle, StringComparison.Ordinal);
            if (idx < 0) return result;
            int bracket = json.IndexOf('[', idx + needle.Length);
            if (bracket < 0) return result;
            int end = json.IndexOf(']', bracket);
            if (end < 0) return result;
            string inner = json.Substring(bracket + 1, end - bracket - 1);
            int i = 0;
            while (i < inner.Length)
            {
                int qs = inner.IndexOf('"', i);
                if (qs < 0) break;
                int qe = inner.IndexOf('"', qs + 1);
                if (qe < 0) break;
                result.Add(inner.Substring(qs + 1, qe - qs - 1));
                i = qe + 1;
            }
            return result;
        }

        private static string Err(string msg)
            => "{\"success\":false,\"error\":\"" +
               msg.Replace("\\", "\\\\").Replace("\"", "\\\"")
                  .Replace("\r", "").Replace("\n", " ") + "\"}";

        private static string Json(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                           .Replace("\r", "\\r").Replace("\n", "\\n")
                           .Replace("\t", "\\t") + "\"";
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    }
}
