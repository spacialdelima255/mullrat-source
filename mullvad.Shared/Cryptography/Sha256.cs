using System.Security.Cryptography;
using System.Text;

namespace mullvad.Cryptography
{
    public static class Sha256
    {
        public static byte[] Hash(byte[] data)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(data);
        }

        public static byte[] Hash(string text, Encoding? encoding = null)
        {
            byte[] bytes = (encoding ?? Encoding.UTF8).GetBytes(text);
            using var sha = SHA256.Create();
            return sha.ComputeHash(bytes);
        }

        public static string HashHex(byte[] data)
        {
            byte[] h = Hash(data);
            var sb = new StringBuilder(h.Length * 2);
            foreach (byte b in h) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public static string HashHex(string text, Encoding? encoding = null)
            => HashHex((encoding ?? Encoding.UTF8).GetBytes(text));

        public static byte[] Hmac(byte[] key, byte[] data)
        {
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(data);
        }

        public static string HmacHex(byte[] key, byte[] data)
        {
            byte[] mac = Hmac(key, data);
            var sb = new StringBuilder(mac.Length * 2);
            foreach (byte b in mac) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
