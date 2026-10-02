using System;
using System.Text;

namespace mullvad.Extensions
{
    public static class ByteExtensions
    {
        public static string ToHexString(this byte[] data)
        {
            if (data == null) return string.Empty;
            var sb = new StringBuilder(data.Length * 2);
            foreach (byte b in data) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public static string ToBase64(this byte[] data) =>
            data == null ? string.Empty : Convert.ToBase64String(data);

        public static byte[] XorWith(this byte[] data, byte[] key)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (key  == null || key.Length == 0) return (byte[])data.Clone();
            var result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++) result[i] = (byte)(data[i] ^ key[i % key.Length]);
            return result;
        }

        public static byte[] FromHexString(this string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Array.Empty<byte>();
            if (hex.Length % 2 != 0) throw new ArgumentException("Odd hex length.");
            var result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return result;
        }
    }
}
