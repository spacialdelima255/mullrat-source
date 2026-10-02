using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace mullvad.Extensions
{
    public static class StreamExtensions
    {
        /// <summary>
        /// Reads exactly <paramref name="count"/> bytes, blocking until all are available
        /// or the stream ends. Returns false when the stream ended before <paramref name="count"/>
        /// bytes could be read.
        /// </summary>
        public static async Task<bool> ReadExactAsync(this Stream stream, byte[] buffer,
            int offset, int count, CancellationToken ct = default)
        {
            int remaining = count;
            while (remaining > 0)
            {
                int read = await stream.ReadAsync(buffer, offset + (count - remaining),
                    remaining, ct).ConfigureAwait(false);
                if (read == 0) return false;
                remaining -= read;
            }
            return true;
        }

        /// <summary>Writes <paramref name="data"/> in full, flushing afterwards.</summary>
        public static async Task WriteAllAsync(this Stream stream, byte[] data,
            CancellationToken ct = default)
        {
            await stream.WriteAsync(data, 0, data.Length, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }

        public static async Task<byte[]> ReadToEndAsync(this Stream stream,
            CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            var buf = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buf, 0, buf.Length, ct)
                               .ConfigureAwait(false)) > 0)
                ms.Write(buf, 0, read);
            return ms.ToArray();
        }
    }
}
