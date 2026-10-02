using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Text.Json;
using mullvad.Models;
using mullvad.Protocol;

namespace mullvad.Network
{
    // Connects to a VPS running in host mode and presents all its clients
    // as if they were local connections, using virtual ClientHandlers backed
    // by in-memory pipes.
    public sealed class VpsOperatorClient
    {
        // Same event interface as TcpServer so Form1 subscribes identically
        public event Action<ClientHandler>?         ClientConnected;
        public event Action<ClientHandler>?         ClientDisconnected;
        public event Action<ClientHandler>?         ClientHandshake;
        public event Action<Exception>?             Error;

        private SslStream?                _ssl;
        private SemaphoreSlim             _sendLock = new(1, 1);
        private CancellationTokenSource?  _cts;

        // vpsServerId → virtual entry
        private readonly ConcurrentDictionary<string, VirtualEntry> _entries = new();

        public bool   IsConnected   { get; private set; }
        public string RemoteAddress { get; private set; } = "";

        // ── Connect / Disconnect ──────────────────────────────────────────────

        public async Task ConnectAsync(string host, int port, string password, CancellationToken ct = default)
        {
            var tcp = new TcpClient { NoDelay = true };
            SslStream? ssl = null;
            try
            {
                await tcp.ConnectAsync(host, port, ct);

                ssl = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false,
                    (_, _, _, _) => true);   // trust any self-signed cert
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = host,
                }, ct);

                _ssl          = ssl;
                ssl           = null;   // ownership transferred
                RemoteAddress = $"{host}:{port}";
                var old = _cts;
                old?.Cancel();
                old?.Dispose();
                _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

                // Send auth
                await _ssl.WriteAsync(Packet.Create(PacketType.VpsAuth, new { password }).Serialize(), _cts.Token);

                var resp = await Packet.ReadAsync(_ssl, _cts.Token);
                if (resp is null || resp.Type == PacketType.VpsAuthFail)
                    throw new Exception(resp?.Type == PacketType.VpsAuthFail
                        ? $"VPS authentication failed: {GetStr(resp.Json, "reason")}"
                        : "VPS did not respond to auth");
                if (resp.Type != PacketType.VpsAuthOk)
                    throw new Exception("Unexpected response from VPS");

                IsConnected = true;
                _ = ReadLoopAsync(_cts.Token);
            }
            catch
            {
                ssl?.Dispose();
                tcp.Close();
                throw;
            }
        }

        public void Disconnect()
        {
            IsConnected = false;
            _cts?.Cancel();
            try { _ssl?.Dispose(); } catch { }
            DisconnectAllVirtuals();
        }

        // ── Read loop ─────────────────────────────────────────────────────────

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var pkt = await Packet.ReadAsync(_ssl!, ct);
                    if (pkt is null) break;

                    switch (pkt.Type)
                    {
                        case PacketType.VpsClientList:  HandleClientList(pkt.Json, ct);  break;
                        case PacketType.VpsClientJoin:  HandleClientJoin(pkt.Json, ct);  break;
                        case PacketType.VpsClientLeave: HandleClientLeave(pkt.Json);     break;
                        case PacketType.VpsRelay:       HandleRelay(pkt.Json);           break;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (!ct.IsCancellationRequested) { Error?.Invoke(ex); }
            finally
            {
                IsConnected = false;
                DisconnectAllVirtuals();
            }
        }

        // ── Protocol handlers ─────────────────────────────────────────────────

        private void HandleClientList(string json, CancellationToken ct)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("clients", out var arr)) return;
                foreach (var el in arr.EnumerateArray())
                    CreateVirtualHandler(el, ct);
            }
            catch { }
        }

        private void HandleClientJoin(string json, CancellationToken ct)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                CreateVirtualHandler(doc.RootElement, ct);
            }
            catch { }
        }

        private void HandleClientLeave(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var id = doc.RootElement.TryGetProperty("id", out var p) ? p.GetString() ?? "" : "";
                if (_entries.TryRemove(id, out var entry))
                    entry.Pipe.Complete();  // closes handler's stream → RunAsync exits → Disconnected fires
            }
            catch { }
        }

        private void HandleRelay(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var id  = doc.RootElement.TryGetProperty("id",   out var ip) ? ip.GetString() ?? "" : "";
                var b64 = doc.RootElement.TryGetProperty("data", out var dp) ? dp.GetString() ?? "" : "";

                if (!_entries.TryGetValue(id, out var entry)) return;
                entry.Pipe.FeedInbound(Convert.FromBase64String(b64));
            }
            catch { }
        }

        // ── Virtual handler creation ──────────────────────────────────────────

        private void CreateVirtualHandler(JsonElement el, CancellationToken ct)
        {
            var vpsId = GetStr(el, "id");
            if (string.IsNullOrEmpty(vpsId)) return;
            if (_entries.ContainsKey(vpsId))  return;  // already exists

            var pipe    = new VirtualClientPipe();
            var handler = new ClientHandler(pipe.HandlerStream, GetStr(el, "ip"));

            // Populate info from the VPS-supplied data
            handler.Info.Computer     = GetStr(el, "computer");
            handler.Info.Username     = GetStr(el, "username");
            handler.Info.Os           = GetStr(el, "os");
            handler.Info.OsEdition    = GetStr(el, "os_edition");
            handler.Info.Architecture = GetStr(el, "architecture");
            handler.Info.Country      = GetStr(el, "country");
            handler.Info.Version      = GetStr(el, "version");
            handler.Info.IsConnected  = true;

            var entry = new VirtualEntry(vpsId, handler, pipe);
            if (!_entries.TryAdd(vpsId, entry)) { pipe.Complete(); return; }

            // When handler disconnects (local disconnect or pipe closed):
            handler.Disconnected += h =>
            {
                bool wasPresent = _entries.TryRemove(vpsId, out _);
                ClientDisconnected?.Invoke(h);

                // If WE initiated the disconnect (entry still existed), tell the VPS
                if (wasPresent)
                    _ = SendRelayAsync(vpsId, Packet.Create(PacketType.Disconnect));
            };

            // Start the handler's read loop in the background
            _ = handler.RunAsync(ct);

            // Start forwarding the handler's outgoing packets to the VPS
            _ = ForwardOutboundAsync(entry, ct);

            // Notify Form1 — ClientConnected then immediately ClientHandshake (info already populated)
            ClientConnected?.Invoke(handler);
            ClientHandshake?.Invoke(handler);
        }

        // Forward packets the virtual handler sends back toward the VPS
        private async Task ForwardOutboundAsync(VirtualEntry entry, CancellationToken ct)
        {
            try
            {
                var relayStream = entry.Pipe.RelayStream;
                while (!ct.IsCancellationRequested)
                {
                    var pkt = await Packet.ReadAsync(relayStream, ct);
                    if (pkt is null) break;

                    // Pong is handled locally; skip to avoid double-pong at VPS
                    if (pkt.Type == PacketType.Pong) continue;

                    await SendRelayAsync(entry.VpsId, pkt);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        // ── Send relay to VPS ─────────────────────────────────────────────────

        private async Task SendRelayAsync(string vpsId, Packet pkt)
        {
            if (_ssl is null || !IsConnected) return;
            var raw   = pkt.Serialize();
            var relay = Packet.CreateRaw(PacketType.VpsRelay,
                "{\"id\":\"" + Esc(vpsId) + "\",\"data\":\"" + Convert.ToBase64String(raw) + "\"}");

            await _sendLock.WaitAsync();
            try { await _ssl.WriteAsync(relay.Serialize()); }
            catch { }
            finally { _sendLock.Release(); }
        }

        // ── Cleanup ───────────────────────────────────────────────────────────

        private void DisconnectAllVirtuals()
        {
            foreach (var entry in _entries.Values)
                entry.Pipe.Complete();
            _entries.Clear();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string GetStr(JsonElement el, string key)
            => el.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";

        private static string GetStr(string json, string key)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                return GetStr(doc.RootElement, key);
            }
            catch { return ""; }
        }

        private static string Esc(string s)
        {
            if (!s.Contains('"') && !s.Contains('\\')) return s;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        // ── VirtualEntry ──────────────────────────────────────────────────────

        private sealed class VirtualEntry
        {
            public string           VpsId   { get; }
            public ClientHandler    Handler { get; }
            public VirtualClientPipe Pipe   { get; }

            public VirtualEntry(string vpsId, ClientHandler handler, VirtualClientPipe pipe)
            {
                VpsId   = vpsId;
                Handler = handler;
                Pipe    = pipe;
            }
        }
    }
}
