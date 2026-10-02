using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using mullvad.Protocol;

namespace mullvad.Network
{
    public sealed class TcpServer
    {
        private TcpListener?             _listener;
        private CancellationTokenSource? _cts;
        private readonly X509Certificate2 _cert;

        public int  Port      { get; }
        public bool IsRunning { get; private set; }

        public event Action<ClientHandler>?         ClientConnected;
        public event Action<ClientHandler>?         ClientDisconnected;
        public event Action<ClientHandler>?         ClientHandshake;
        public event Action<ClientHandler, Packet>? PacketReceived;
        public event Action<Exception>?             Error;

        public TcpServer(int port = 7777)
        {
            Port  = port;
            _cert = CertificateHelper.GetOrCreate();
        }

        public void Start()
        {
            if (IsRunning) return;
            _cts      = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, Port);
            _listener.Start(backlog: 200);
            IsRunning = true;
            _ = AcceptLoopAsync(_cts.Token);
        }

        public void Stop()
        {
            if (!IsRunning) return;
            _cts?.Cancel();
            _listener?.Stop();
            IsRunning = false;
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var tcp = await _listener!.AcceptTcpClientAsync(ct);
                    ConfigureSocket(tcp);
                    _ = HandshakeThenRegisterAsync(tcp, ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    if (ct.IsCancellationRequested) break;
                    Error?.Invoke(ex);
                    // Brief backoff on transient errors — keep the loop alive
                    try { await Task.Delay(500, ct); } catch { break; }
                }
            }

            IsRunning = false;
        }

        private static void ConfigureSocket(TcpClient tcp)
        {
            tcp.NoDelay           = true;
            tcp.SendBufferSize    = 65536;
            tcp.ReceiveBufferSize = 65536;
            // TCP keepalive: detect dead peers within ~30s (idle 10s, probe every 5s, 4 probes)
            tcp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            try
            {
                // IOControl: idle=10s, interval=5s  (Windows-specific; silently ignored elsewhere)
                var ka = new byte[12];
                BitConverter.GetBytes(1u).CopyTo(ka, 0);   // on
                BitConverter.GetBytes(10_000u).CopyTo(ka, 4); // idle ms
                BitConverter.GetBytes(5_000u).CopyTo(ka, 8);  // interval ms
                tcp.Client.IOControl(System.Net.Sockets.IOControlCode.KeepAliveValues, ka, null);
            }
            catch { }
        }

        private async Task HandshakeThenRegisterAsync(TcpClient tcp, CancellationToken ct)
        {
            try
            {
                // 30-second hard cap on the TLS handshake — prevents slow/malicious clients
                // from tying up a connection slot indefinitely.
                using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                handshakeCts.CancelAfter(TimeSpan.FromSeconds(30));

                var ssl = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false);
                await ssl.AuthenticateAsServerAsync(
                    new SslServerAuthenticationOptions
                    {
                        ServerCertificate        = _cert,
                        ClientCertificateRequired = false,
                    }, handshakeCts.Token);

                var handler               = new ClientHandler(tcp, ssl);
                handler.Disconnected      += h      => ClientDisconnected?.Invoke(h);
                handler.HandshakeReceived += h      => ClientHandshake?.Invoke(h);
                handler.PacketReceived    += (h, p) => PacketReceived?.Invoke(h, p);

                ClientConnected?.Invoke(handler);
                _ = handler.RunAsync(ct);
            }
            catch { try { tcp.Close(); } catch { } }
        }
    }
}
