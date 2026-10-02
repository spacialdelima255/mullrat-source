using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;

namespace mullvad.Module.InstalledApplications
{
    public sealed class InstalledApplicationsClientModule
    {
        public static string ModuleId => "mullvad.installedapps";

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "list": return ListApps();
                    default:     return Err("Unknown action: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ── List ─────────────────────────────────────────────────────────────

        private static string ListApps()
        {
            var apps = new List<AppEntry>();

            ReadUninstallKey(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",            apps);
            ReadUninstallKey(Registry.LocalMachine, @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall", apps);
            ReadUninstallKey(Registry.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",            apps);

            // Sort by display name
            apps.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            var sb = new StringBuilder("[");
            bool first = true;
            foreach (var a in apps)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"name\":").Append(Json(a.Name))
                  .Append(",\"version\":").Append(Json(a.Version))
                  .Append(",\"publisher\":").Append(Json(a.Publisher))
                  .Append(",\"install_date\":").Append(Json(a.InstallDate))
                  .Append(",\"install_location\":").Append(Json(a.InstallLocation))
                  .Append("}");
            }
            sb.Append("]");
            return sb.ToString();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void ReadUninstallKey(RegistryKey hive, string subPath, List<AppEntry> apps)
        {
            try
            {
                using var key = hive.OpenSubKey(subPath, false);
                if (key == null) return;

                foreach (var subName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var sub = key.OpenSubKey(subName, false);
                        if (sub == null) continue;

                        string name = sub.GetValue("DisplayName") as string ?? "";
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        // Skip system components and updates
                        var isSystemComponent = sub.GetValue("SystemComponent");
                        if (isSystemComponent is int sc && sc == 1) continue;

                        string parentKey = sub.GetValue("ParentKeyName") as string ?? "";
                        if (!string.IsNullOrEmpty(parentKey)) continue; // sub-component of another product

                        apps.Add(new AppEntry
                        {
                            Name            = name,
                            Version         = sub.GetValue("DisplayVersion") as string ?? "",
                            Publisher       = sub.GetValue("Publisher") as string ?? "",
                            InstallDate     = FormatDate(sub.GetValue("InstallDate") as string ?? ""),
                            InstallLocation = sub.GetValue("InstallLocation") as string ?? "",
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static string FormatDate(string raw)
        {
            // Registry stores dates as "YYYYMMDD"
            if (raw.Length == 8 &&
                int.TryParse(raw.Substring(0, 4), out int y) &&
                int.TryParse(raw.Substring(4, 2), out int mo) &&
                int.TryParse(raw.Substring(6, 2), out int d))
            {
                try { return new DateTime(y, mo, d).ToString("yyyy-MM-dd"); } catch { }
            }
            return raw;
        }

        private struct AppEntry
        {
            public string Name, Version, Publisher, InstallDate, InstallLocation;
        }

        // ── JSON ─────────────────────────────────────────────────────────────

        private static string Err(string msg) => "{\"error\":" + Json(msg) + "}";

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
