using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace mullvad.Protocol
{
    // Wire format:
    //   [4 bytes] payload length  (includes the 1-byte type field)
    //   [1 byte]  PacketType
    //   [N bytes] UTF-8 JSON body
    public sealed class Packet
    {
        public PacketType Type { get; }
        public string     Json { get; }

        private Packet(PacketType type, string json)
        {
            Type = type;
            Json = json;
        }

        public static Packet Create(PacketType type, object? payload = null)
        {
            var json = payload is null ? "{}" : JsonSerializer.Serialize(payload);
            return new Packet(type, json);
        }

        public byte[] Serialize()
        {
            var body   = Encoding.UTF8.GetBytes(Json);
            var buffer = new byte[4 + 1 + body.Length];
            BitConverter.GetBytes(1 + body.Length).CopyTo(buffer, 0);
            buffer[4] = (byte)Type;
            body.CopyTo(buffer, 5);
            return buffer;
        }

        public static async Task<Packet?> ReadAsync(NetworkStream stream, CancellationToken ct)
        {
            var lenBuf = new byte[4];
            if (!await ReadExactAsync(stream, lenBuf, ct)) return null;

            var len = BitConverter.ToInt32(lenBuf, 0);
            if (len < 1 || len > 256 * 1024 * 1024) return null;

            var payload = new byte[len];
            if (!await ReadExactAsync(stream, payload, ct)) return null;

            var type = (PacketType)payload[0];
            var json = Encoding.UTF8.GetString(payload, 1, payload.Length - 1);
            return new Packet(type, json);
        }

        private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buf, CancellationToken ct)
        {
            int offset = 0;
            while (offset < buf.Length)
            {
                int n = await stream.ReadAsync(buf.AsMemory(offset), ct);
                if (n == 0) return false;
                offset += n;
            }
            return true;
        }
    }
}
