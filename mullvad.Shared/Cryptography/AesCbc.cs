using System.Security.Cryptography;
using System.Text;

namespace mullvad.Cryptography
{
    /// <summary>
    /// AES-256-CBC authenticated encryption with HMAC-SHA256.
    /// Wire layout: [32-byte HMAC][16-byte IV][ciphertext]
    /// Both the AES key and the HMAC key are derived from the caller's passphrase
    /// via PBKDF2-SHA1 (50 000 iterations) against a fixed salt.
    /// </summary>
    public static class AesCbc
    {
        private const int IvBytes    = 16;
        private const int HmacBytes  = 32;
        private const int KeyBytes   = 32;
        private const int Iterations = 50_000;

        // Fixed 128-bit salt — uniquely scopes all derived keys to this application.
        private static readonly byte[] _salt =
        {
            0xBF, 0xEB, 0x1E, 0x56, 0xFB, 0xCD, 0x97, 0x3B,
            0xB2, 0x19, 0x02, 0x24, 0x30, 0xA5, 0x78, 0x43
        };

        public static byte[] Encrypt(byte[] plaintext, string passphrase)
        {
            DeriveKeys(passphrase, out byte[] aesKey, out byte[] hmacKey);

            byte[] iv = new byte[IvBytes];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(iv);

            byte[] cipher;
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256; aes.Key = aesKey; aes.IV = iv;
                aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                using var enc = aes.CreateEncryptor();
                cipher = enc.TransformFinalBlock(plaintext, 0, plaintext.Length);
            }

            // payload = IV || ciphertext
            byte[] payload = new byte[IvBytes + cipher.Length];
            Buffer.BlockCopy(iv,     0, payload, 0,       IvBytes);
            Buffer.BlockCopy(cipher, 0, payload, IvBytes, cipher.Length);

            byte[] mac = Hmac(hmacKey, payload);

            // result = HMAC || payload
            byte[] result = new byte[HmacBytes + payload.Length];
            Buffer.BlockCopy(mac,     0, result, 0,         HmacBytes);
            Buffer.BlockCopy(payload, 0, result, HmacBytes, payload.Length);
            return result;
        }

        public static byte[]? Decrypt(byte[] data, string passphrase)
        {
            if (data == null || data.Length < HmacBytes + IvBytes + 1)
                return null;

            DeriveKeys(passphrase, out byte[] aesKey, out byte[] hmacKey);

            byte[] mac     = new byte[HmacBytes];
            byte[] payload = new byte[data.Length - HmacBytes];
            Buffer.BlockCopy(data, 0,         mac,     0, HmacBytes);
            Buffer.BlockCopy(data, HmacBytes, payload, 0, payload.Length);

            if (!SecureCompare.Equals(mac, Hmac(hmacKey, payload)))
                return null;

            byte[] iv     = new byte[IvBytes];
            byte[] cipher = new byte[payload.Length - IvBytes];
            Buffer.BlockCopy(payload, 0,       iv,     0, IvBytes);
            Buffer.BlockCopy(payload, IvBytes, cipher, 0, cipher.Length);

            try
            {
                using var aes = Aes.Create();
                aes.KeySize = 256; aes.Key = aesKey; aes.IV = iv;
                aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                using var dec = aes.CreateDecryptor();
                return dec.TransformFinalBlock(cipher, 0, cipher.Length);
            }
            catch { return null; }
        }

        // Convenience overloads for UTF-8 strings
        public static string EncryptString(string plaintext, string passphrase)
        {
            byte[] enc = Encrypt(Encoding.UTF8.GetBytes(plaintext), passphrase);
            return Convert.ToBase64String(enc);
        }

        public static string? DecryptString(string base64Ciphertext, string passphrase)
        {
            try
            {
                byte[] data    = Convert.FromBase64String(base64Ciphertext);
                byte[]? plain  = Decrypt(data, passphrase);
                return plain is null ? null : Encoding.UTF8.GetString(plain);
            }
            catch { return null; }
        }

        private static void DeriveKeys(string passphrase, out byte[] aesKey, out byte[] hmacKey)
        {
            byte[] pwd = Encoding.UTF8.GetBytes(passphrase);
            using var kdf = new Rfc2898DeriveBytes(pwd, _salt, Iterations);
            aesKey  = kdf.GetBytes(KeyBytes);
            hmacKey = kdf.GetBytes(KeyBytes);
        }

        private static byte[] Hmac(byte[] key, byte[] data)
        {
            using var h = new HMACSHA256(key);
            return h.ComputeHash(data);
        }
    }
}
