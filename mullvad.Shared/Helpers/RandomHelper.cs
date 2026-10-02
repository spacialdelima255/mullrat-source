using System;
using System.Security.Cryptography;
using System.Text;

namespace mullvad.Helpers
{
    public static class RandomHelper
    {
        private const string _alphaNum =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        public static byte[] GetBytes(int count)
        {
            byte[] buf = new byte[count];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(buf);
            return buf;
        }

        public static string GetString(int length, string? chars = null)
        {
            string pool = chars ?? _alphaNum;
            byte[] raw  = GetBytes(length);
            var    sb   = new StringBuilder(length);
            foreach (byte b in raw) sb.Append(pool[b % pool.Length]);
            return sb.ToString();
        }

        public static string GetHex(int bytes)
        {
            byte[] raw = GetBytes(bytes);
            var    sb  = new StringBuilder(raw.Length * 2);
            foreach (byte b in raw) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public static Guid NewGuid()
        {
            byte[] b = GetBytes(16);
            // Version 4, variant 1
            b[6] = (byte)((b[6] & 0x0F) | 0x40);
            b[8] = (byte)((b[8] & 0x3F) | 0x80);
            return new Guid(b);
        }
    }
}
