using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace mullvad.Protocol
{
    // Wire format (unchanged):
    //   [4 bytes LE] payload length  (1-byte type + N-byte UTF-8 JSON body)
    //   [1 byte]     PacketType
    //   [N bytes]    UTF-8 JSON body
    public sealed class Packet
    {
        private static readonly JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            ContractResolver    = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling   = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include
        };

        public PacketType Type { get; }
        public string     Json { get; }

        private Packet(PacketType type, string json)
        {
            Type = type;
            Json = json;
        }

        // --- Construction ----------------------------------------------------------

        public static Packet Create(PacketType type, object? payload = null)
        {
            string json = payload is null ? "{}" : JsonConvert.SerializeObject(payload, _settings);
            return new Packet(type, json);
        }

        public static Packet CreateRaw(PacketType type, string json) =>
            new Packet(type, json);

        // --- Deserialization helpers ------------------------------------------------

        public T? Get<T>() where T : class
        {
            try   { return JsonConvert.DeserializeObject<T>(Json, _settings); }
            catch { return null; }
        }

        public T GetRequired<T>() where T : class
        {
            var result = JsonConvert.DeserializeObject<T>(Json, _settings);
            if (result is null) throw new InvalidOperationException(
                $"Packet body could not be deserialized as {typeof(T).Name}.");
            return result;
        }

        public bool TryGet<T>(out T? value) where T : class
        {
            try   { value = JsonConvert.DeserializeObject<T>(Json, _settings); return value != null; }
            catch { value = null; return false; }
        }

        // --- Wire I/O --------------------------------------------------------------

        public byte[] Serialize()
        {
            byte[] body   = Encoding.UTF8.GetBytes(Json);
            byte[] buffer = new byte[4 + 1 + body.Length];
            int    len    = 1 + body.Length;

            // Little-endian 4-byte length (matches original BitConverter.GetBytes)
            buffer[0] = (byte)(len);
            buffer[1] = (byte)(len >> 8);
            buffer[2] = (byte)(len >> 16);
            buffer[3] = (byte)(len >> 24);

            buffer[4] = (byte)Type;
            Buffer.BlockCopy(body, 0, buffer, 5, body.Length);
            return buffer;
        }

        public static async Task<Packet?> ReadAsync(Stream stream, CancellationToken ct)
        {
            var lenBuf = new byte[4];
            if (!await ReadExactAsync(stream, lenBuf, ct).ConfigureAwait(false)) return null;

            int len = lenBuf[0] | (lenBuf[1] << 8) | (lenBuf[2] << 16) | (lenBuf[3] << 24);
            if (len < 1 || len > 256 * 1024 * 1024) return null;

            var payload = new byte[len];
            if (!await ReadExactAsync(stream, payload, ct).ConfigureAwait(false)) return null;

            var type = (PacketType)payload[0];
            var json = Encoding.UTF8.GetString(payload, 1, payload.Length - 1);
            return new Packet(type, json);
        }

        private static async Task<bool> ReadExactAsync(Stream stream, byte[] buf,
            CancellationToken ct)
        {
            int offset = 0;
            while (offset < buf.Length)
            {
                int n = await stream.ReadAsync(buf, offset, buf.Length - offset, ct)
                                    .ConfigureAwait(false);
                if (n == 0) return false;
                offset += n;
            }
            return true;
        }
    }
}
