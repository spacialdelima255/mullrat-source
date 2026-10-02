// language: C#, target: net472, file: BrowserInspectionModule.cs
// module id: mullvad.browserinspect
// Enumerates installed browsers, their profiles, and versions.
// action "inspect"     → JSON with all browsers + profiles + base64 icon
// action "open_folder" → opens explorer.exe at given path on the client
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace mullvad.Module.BrowserInspection
{
    public class BrowserInspectionModule
    {
        public static string ModuleId => "mullvad.browserinspect";

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "inspect":     return Inspect();
                    case "open_folder": return OpenFolder(payload);
                    default:            return Err("unknown action");
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Browser definitions
        // ─────────────────────────────────────────────────────────────────────
        private struct BrowserDef
        {
            public string Name;
            public string ExePath;
            public string DataPath;
            public string Type; // "chromium" | "firefox"

            public BrowserDef(string name, string exe, string data, string type)
            { Name = name; ExePath = exe; DataPath = data; Type = type; }
        }

        private static readonly BrowserDef[] _defs = new[]
        {
            new BrowserDef("Google Chrome",
                @"%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe",
                @"%LOCALAPPDATA%\Google\Chrome\User Data", "chromium"),
            new BrowserDef("Microsoft Edge",
                @"%LOCALAPPDATA%\Microsoft\Edge\Application\msedge.exe",
                @"%LOCALAPPDATA%\Microsoft\Edge\User Data", "chromium"),
            new BrowserDef("Brave",
                @"%LOCALAPPDATA%\BraveSoftware\Brave-Browser\Application\brave.exe",
                @"%LOCALAPPDATA%\BraveSoftware\Brave-Browser\User Data", "chromium"),
            new BrowserDef("Vivaldi",
                @"%LOCALAPPDATA%\Vivaldi\Application\vivaldi.exe",
                @"%LOCALAPPDATA%\Vivaldi\User Data", "chromium"),
            new BrowserDef("Yandex Browser",
                @"%LOCALAPPDATA%\Yandex\YandexBrowser\Application\browser.exe",
                @"%LOCALAPPDATA%\Yandex\YandexBrowser\User Data", "chromium"),
            new BrowserDef("Opera",
                @"%LOCALAPPDATA%\Programs\Opera\opera.exe",
                @"%APPDATA%\Opera Software\Opera Stable", "chromium"),
            new BrowserDef("Opera GX",
                @"%LOCALAPPDATA%\Programs\Opera GX\opera.exe",
                @"%APPDATA%\Opera Software\Opera GX Stable", "chromium"),
            new BrowserDef("Mozilla Firefox",
                @"%ProgramFiles%\Mozilla Firefox\firefox.exe",
                @"%APPDATA%\Mozilla\Firefox", "firefox"),
            new BrowserDef("Mozilla Firefox",
                @"%ProgramFiles(x86)%\Mozilla Firefox\firefox.exe",
                @"%APPDATA%\Mozilla\Firefox", "firefox"),
        };

        // ─────────────────────────────────────────────────────────────────────
        //  Inspect
        // ─────────────────────────────────────────────────────────────────────
        private static string Inspect()
        {
            var found   = new List<BrowserResult>();
            var seenExe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var def in _defs)
            {
                string exe  = Environment.ExpandEnvironmentVariables(def.ExePath);
                string data = Environment.ExpandEnvironmentVariables(def.DataPath);

                if (!File.Exists(exe)) continue;
                if (!seenExe.Add(exe)) continue; // same exe seen via multiple defs (e.g. firefox x86/x64)

                string version  = GetVersion(exe);
                string iconB64  = GetIconB64(exe);
                var    profiles = def.Type == "firefox"
                    ? GetFirefoxProfiles(data)
                    : GetChromiumProfiles(data);

                found.Add(new BrowserResult
                {
                    Name     = def.Name,
                    Version  = version,
                    Exe      = exe,
                    DataPath = data,
                    IconB64  = iconB64,
                    Profiles = profiles,
                });
            }

            return BuildJson(found);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Open Folder
        // ─────────────────────────────────────────────────────────────────────
        private static string OpenFolder(string payload)
        {
            string path = ExtractStr(payload, "path");
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return Err("path not found: " + path);

            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"")
            {
                UseShellExecute = true,
            });
            return "{\"status\":\"opened\"}";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Profile enumeration
        // ─────────────────────────────────────────────────────────────────────
        private static List<ProfileInfo> GetChromiumProfiles(string userDataDir)
        {
            var list = new List<ProfileInfo>();
            if (!Directory.Exists(userDataDir)) return list;

            foreach (string dir in Directory.GetDirectories(userDataDir))
            {
                string n = Path.GetFileName(dir);
                if (!n.Equals("Default", StringComparison.OrdinalIgnoreCase) &&
                    !(n.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) && n.Length > 8))
                    continue;

                string display = n;
                string email   = "";
                string prefPath = Path.Combine(dir, "Preferences");

                if (File.Exists(prefPath))
                {
                    try
                    {
                        string prefs = File.ReadAllText(prefPath, Encoding.UTF8);
                        // Profile display name
                        var nm = Regex.Match(prefs, "\"name\"\\s*:\\s*\"([^\"]+)\"");
                        if (nm.Success) display = nm.Groups[1].Value;
                        // Account email (first match under account_info)
                        var em = Regex.Match(prefs, "\"email\"\\s*:\\s*\"([^\"@]+@[^\"]+)\"");
                        if (em.Success) email = em.Groups[1].Value;
                    }
                    catch { }
                }

                list.Add(new ProfileInfo { FolderName = n, Display = display, Email = email, Path = dir });
            }

            return list;
        }

        private static List<ProfileInfo> GetFirefoxProfiles(string appDataMozilla)
        {
            var list    = new List<ProfileInfo>();
            string ini  = Path.Combine(appDataMozilla, "profiles.ini");
            if (!File.Exists(ini)) return list;

            try
            {
                string content  = File.ReadAllText(ini, Encoding.UTF8);
                var    sections = Regex.Matches(content, @"\[Profile\d+\]([\s\S]*?)(?=\[|$)");
                foreach (Match sec in sections)
                {
                    string body = sec.Groups[1].Value;
                    var    nameM = Regex.Match(body, @"(?m)^Name=(.+)$");
                    var    pathM = Regex.Match(body, @"(?m)^Path=(.+)$");
                    var    relM  = Regex.Match(body, @"(?m)^IsRelative=(\d)$");
                    if (!nameM.Success || !pathM.Success) continue;

                    string name    = nameM.Groups[1].Value.Trim();
                    string rawPath = pathM.Groups[1].Value.Trim();
                    bool   isRel   = relM.Success && relM.Groups[1].Value == "1";
                    string full    = isRel
                        ? Path.Combine(appDataMozilla, rawPath.Replace('/', Path.DirectorySeparatorChar))
                        : rawPath.Replace('/', Path.DirectorySeparatorChar);

                    list.Add(new ProfileInfo { FolderName = name, Display = name, Email = "", Path = full });
                }
            }
            catch { }

            return list;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Icon extraction
        // ─────────────────────────────────────────────────────────────────────
        private static string GetIconB64(string exePath)
        {
            try
            {
                using var icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon == null) return "";
                using var bmp  = icon.ToBitmap();
                using var ms   = new MemoryStream();
                // Scale to 32×32 for consistent size
                using var scaled = new Bitmap(32, 32);
                using (var g = Graphics.FromImage(scaled))
                    g.DrawImage(bmp, 0, 0, 32, 32);
                scaled.Save(ms, ImageFormat.Png);
                return Convert.ToBase64String(ms.ToArray());
            }
            catch { return ""; }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Version
        // ─────────────────────────────────────────────────────────────────────
        private static string GetVersion(string exe)
        {
            try
            {
                var fvi = FileVersionInfo.GetVersionInfo(exe);
                return fvi.FileVersion ?? fvi.ProductVersion ?? "";
            }
            catch { return ""; }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  JSON serialization (no System.Text.Json in net472)
        // ─────────────────────────────────────────────────────────────────────
        private static string BuildJson(List<BrowserResult> browsers)
        {
            var sb = new StringBuilder();
            sb.Append("{\"browsers\":[");
            for (int i = 0; i < browsers.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var b = browsers[i];
                sb.Append('{');
                AppendKV(sb, "name",      b.Name);      sb.Append(',');
                AppendKV(sb, "version",   b.Version);   sb.Append(',');
                AppendKV(sb, "exe",       b.Exe);        sb.Append(',');
                AppendKV(sb, "data_path", b.DataPath);  sb.Append(',');
                AppendKV(sb, "icon_b64",  b.IconB64);   sb.Append(',');
                sb.Append("\"profiles\":[");
                for (int j = 0; j < b.Profiles.Count; j++)
                {
                    if (j > 0) sb.Append(',');
                    var p = b.Profiles[j];
                    sb.Append('{');
                    AppendKV(sb, "folder",  p.FolderName); sb.Append(',');
                    AppendKV(sb, "display", p.Display);    sb.Append(',');
                    AppendKV(sb, "email",   p.Email);      sb.Append(',');
                    AppendKV(sb, "path",    p.Path);
                    sb.Append('}');
                }
                sb.Append("]}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static void AppendKV(StringBuilder sb, string key, string value)
        {
            sb.Append('"'); sb.Append(key); sb.Append("\":\"");
            sb.Append(JEsc(value));
            sb.Append('"');
        }

        private static string JEsc(string s)
            => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")
                        .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");

        private static string ExtractStr(string json, string key)
        {
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : "";
        }

        private static string Err(string msg)
            => "{\"error\":\"" + JEsc(msg) + "\"}";

        // ─────────────────────────────────────────────────────────────────────
        //  Data classes
        // ─────────────────────────────────────────────────────────────────────
        private class BrowserResult
        {
            public string             Name     = "";
            public string             Version  = "";
            public string             Exe      = "";
            public string             DataPath = "";
            public string             IconB64  = "";
            public List<ProfileInfo>  Profiles = new List<ProfileInfo>();
        }

        private class ProfileInfo
        {
            public string FolderName = "";
            public string Display    = "";
            public string Email      = "";
            public string Path       = "";
        }
    }
}
