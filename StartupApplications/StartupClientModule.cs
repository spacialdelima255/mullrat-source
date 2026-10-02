using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace mullvad.Module.StartupApplications
{
    public sealed class StartupClientModule
    {
        public static string ModuleId => "mullvad.startupmanager";

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "list":    return ListStartup();
                    case "add":     return AddStartup(payload);
                    case "delete":  return DeleteStartup(payload);
                    case "enable":  return SetEnabled(payload, true);
                    case "disable": return SetEnabled(payload, false);
                    default:        return Err("Unknown action: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ── Actions ──────────────────────────────────────────────────────────

        private static string ListStartup()
        {
            var entries = new List<Entry>();

            ReadRunKey(Registry.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",     "HKCU\\Run",         "HKCU", entries);
            ReadRunKey(Registry.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "HKCU\\RunOnce",     "HKCU", entries);
            ReadRunKey(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",     "HKLM\\Run",         "HKLM", entries);
            ReadRunKey(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "HKLM\\RunOnce",     "HKLM", entries);
            ReadRunKey(Registry.LocalMachine, @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM\\Run (x86)", "HKLM", entries);

            ReadStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup),       "User Startup Folder",   entries);
            ReadStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Common Startup Folder", entries);

            var sb = new StringBuilder("[");
            bool first = true;
            foreach (var e in entries)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"name\":").Append(Json(e.Name))
                  .Append(",\"command\":").Append(Json(e.Command))
                  .Append(",\"location\":").Append(Json(e.Location))
                  .Append(",\"type\":").Append(Json(e.Type))
                  .Append(",\"status\":").Append(Json(e.Status))
                  .Append("}");
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static string AddStartup(string payload)
        {
            var name     = GetStr(payload, "name");
            var command  = GetStr(payload, "command");
            var location = GetStr(payload, "location") ?? "HKCU\\Run";

            if (string.IsNullOrEmpty(name))    return Err("name required");
            if (string.IsNullOrEmpty(command)) return Err("command required");

            var (hive, subPath) = ParseLocation(location);
            if (hive == null) return Err("invalid location: " + location);

            using var key = hive.OpenSubKey(subPath, true);
            if (key == null) return Err("cannot open registry key");

            key.SetValue(name, command, RegistryValueKind.String);
            return "{\"success\":true}";
        }

        private static string DeleteStartup(string payload)
        {
            var name     = GetStr(payload, "name");
            var location = GetStr(payload, "location");

            if (string.IsNullOrEmpty(name))     return Err("name required");
            if (string.IsNullOrEmpty(location)) return Err("location required");

            // Startup folder shortcut
            if (location == "User Startup Folder" || location == "Common Startup Folder")
            {
                var folder = location == "User Startup Folder"
                    ? Environment.GetFolderPath(Environment.SpecialFolder.Startup)
                    : Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
                var lnk = Path.Combine(folder, name + ".lnk");
                if (File.Exists(lnk))
                    File.Delete(lnk);
                return "{\"success\":true}";
            }

            var (hive, subPath) = ParseLocation(location);
            if (hive == null) return Err("invalid location: " + location);

            using var key = hive.OpenSubKey(subPath, true);
            if (key == null) return Err("cannot open registry key");

            key.DeleteValue(name, false);
            return "{\"success\":true}";
        }

        private static string SetEnabled(string payload, bool enable)
        {
            var name     = GetStr(payload, "name");
            var location = GetStr(payload, "location");

            if (string.IsNullOrEmpty(name))     return Err("name required");
            if (string.IsNullOrEmpty(location)) return Err("location required");

            var (hive, subPath) = ParseLocation(location);
            if (hive == null) return Err("invalid location: " + location);

            // Determine the StartupApproved subkey that corresponds to this Run key
            string approvedSub;
            if (subPath.EndsWith("\\Run",     StringComparison.OrdinalIgnoreCase) ||
                subPath.EndsWith("\\RunOnce", StringComparison.OrdinalIgnoreCase))
            {
                approvedSub = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
            }
            else
            {
                return Err("enable/disable not supported for this location");
            }

            try
            {
                using var approvedKey = hive.OpenSubKey(approvedSub, true)
                                     ?? hive.CreateSubKey(approvedSub);
                if (approvedKey == null) return Err("cannot open StartupApproved key");

                // 0x02 = enabled, 0x03 = disabled — 12-byte binary value (Task Manager format)
                byte flag = enable ? (byte)0x02 : (byte)0x03;
                approvedKey.SetValue(name,
                    new byte[] { flag, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
                    RegistryValueKind.Binary);
                return "{\"success\":true}";
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private struct Entry
        {
            public string Name, Command, Location, Type, Status;
        }

        private static void ReadRunKey(RegistryKey hive, string subPath, string locationName,
            string hiveShort, List<Entry> entries)
        {
            try
            {
                using var key = hive.OpenSubKey(subPath, false);
                if (key == null) return;

                // Check StartupApproved for enable/disable state
                var approvedSub   = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
                using var approved = hive.OpenSubKey(approvedSub, false);

                foreach (var name in key.GetValueNames())
                {
                    try
                    {
                        var cmd    = key.GetValue(name)?.ToString() ?? "";
                        var status = "Enabled";

                        if (approved != null)
                        {
                            var val = approved.GetValue(name) as byte[];
                            if (val != null && val.Length >= 1 && val[0] == 0x03)
                                status = "Disabled";
                        }

                        entries.Add(new Entry
                        {
                            Name     = name,
                            Command  = cmd,
                            Location = locationName,
                            Type     = "Registry",
                            Status   = status
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void ReadStartupFolder(string folder, string locationName, List<Entry> entries)
        {
            try
            {
                if (!Directory.Exists(folder)) return;
                foreach (var file in Directory.GetFiles(folder, "*.lnk"))
                {
                    entries.Add(new Entry
                    {
                        Name     = Path.GetFileNameWithoutExtension(file),
                        Command  = file,
                        Location = locationName,
                        Type     = "Folder",
                        Status   = "Enabled"
                    });
                }
            }
            catch { }
        }

        private static (RegistryKey hive, string subPath) ParseLocation(string location)
        {
            switch (location?.ToUpperInvariant())
            {
                case "HKCU\\RUN":
                    return (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                case "HKCU\\RUNONCE":
                    return (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce");
                case "HKLM\\RUN":
                    return (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                case "HKLM\\RUNONCE":
                    return (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce");
                case "HKLM\\RUN (X86)":
                    return (Registry.LocalMachine, @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Run");
                default:
                    return (null, null);
            }
        }

        // ── JSON helpers ─────────────────────────────────────────────────────

        private static string GetStr(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var k   = "\"" + key + "\"";
            int idx = json.IndexOf(k);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + k.Length);
            if (colon < 0) return null;
            int start = colon + 1;
            while (start < json.Length && json[start] == ' ') start++;
            if (start >= json.Length || json[start] != '"') return null;
            start++;
            var sb = new StringBuilder();
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    switch (json[++i])
                    {
                        case '"':  sb.Append('"');  break;
                        case '\\': sb.Append('\\'); break;
                        case 'n':  sb.Append('\n'); break;
                        case 'r':  sb.Append('\r'); break;
                        case 't':  sb.Append('\t'); break;
                        default:   sb.Append(json[i]); break;
                    }
                }
                else if (json[i] == '"') break;
                else sb.Append(json[i]);
            }
            return sb.ToString();
        }

        private static string Err(string msg)  => "{\"error\":" + Json(msg) + "}";

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
