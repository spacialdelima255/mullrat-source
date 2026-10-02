using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using mullvad.Extensions;

namespace mullvad.Networking
{
    /// <summary>
    /// Length-prefixed frame framing for async TCP streams.
    /// Wire format: [4-byte big-endian payload length][payload bytes]
    /// Maximum frame size is 16 MB to guard against OOM from malformed headers.
    /// </summary>
    public static class FrameReader
    {
        public const int MaxFrameBytes = 16 * 1024 * 1024;

        public static async Task WriteFrameAsync(Stream stream, byte[] payload,
            CancellationToken ct = default)
        {
            if (payload.Length > MaxFrameBytes)
                throw new InvalidOperationException(
                    $"Frame too large: {payload.Length} > {MaxFrameBytes}");

            byte[] header = new byte[4];
            header[0] = (byte)(payload.Length >> 24);
            header[1] = (byte)(payload.Length >> 16);
            header[2] = (byte)(payload.Length >> 8);
            header[3] = (byte)(payload.Length);

            await stream.WriteAsync(header, 0, 4, ct).ConfigureAwait(false);
            await stream.WriteAsync(payload, 0, payload.Length, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads one length-prefixed frame. Returns null on clean EOF.
        /// Throws <see cref="IOException"/> if the connection drops mid-frame.
        /// </summary>
        public static async Task<byte[]?> ReadFrameAsync(Stream stream,
            CancellationToken ct = default)
        {
            byte[] header = new byte[4];
            bool ok = await stream.ReadExactAsync(header, 0, 4, ct).ConfigureAwait(false);
            if (!ok) return null; // EOF before a full header → clean disconnect

            int length = (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3];
            if (length < 0 || length > MaxFrameBytes)
                throw new IOException($"Invalid frame length: {length}");

            if (length == 0) return Array.Empty<byte>();

            byte[] payload = new byte[length];
            bool complete = await stream.ReadExactAsync(payload, 0, length, ct)
                                        .ConfigureAwait(false);
            if (!complete)
                throw new IOException("Connection dropped mid-frame.");
            return payload;
        }
    }
}
