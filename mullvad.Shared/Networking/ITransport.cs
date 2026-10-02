using System;
using System.Threading;
using System.Threading.Tasks;

namespace mullvad.Networking
{
    /// <summary>
    /// Abstraction over a single bidirectional transport channel (TCP, named pipe, etc.).
    /// Implementations must be thread-safe for concurrent reads and writes.
    /// </summary>
    public interface ITransport : IDisposable
    {
        string ClientId    { get; }
        bool   IsConnected { get; }

        Task SendAsync(byte[] data, CancellationToken ct = default);
        Task<byte[]?> ReceiveAsync(CancellationToken ct = default);
        void Disconnect();

        event EventHandler<byte[]>?   OnMessageReceived;
        event EventHandler<Exception>? OnError;
        event EventHandler?            OnDisconnected;
    }
}
