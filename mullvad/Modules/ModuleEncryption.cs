using System.Security.Cryptography;
using System.Text;

namespace mullvad.Modules;

internal static class ModuleEncryption
{
    private const string KeySalt  = "mullvad-modules-v1";
    private const int    KeyLen   = 32;
    private const int    NonceLen = 12;
    private const int    TagLen   = 16;

    private static readonly byte[] _key = DeriveKey();

    private static byte[] DeriveKey()
    {
        var pw   = Encoding.UTF8.GetBytes(Environment.MachineName.ToUpperInvariant());
        var salt = Encoding.UTF8.GetBytes(KeySalt);
        using var kdf = new Rfc2898DeriveBytes(pw, salt, 10_000, HashAlgorithmName.SHA256);
        return kdf.GetBytes(KeyLen);
    }

    // Layout: [nonce:12][tag:16][ciphertext]
    public static byte[] Encrypt(byte[] plaintext)
    {
        var nonce = new byte[NonceLen];
        RandomNumberGenerator.Fill(nonce);

        var tag        = new byte[TagLen];
        var ciphertext = new byte[plaintext.Length];

        using var aes = new AesGcm(_key, TagLen);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var result = new byte[NonceLen + TagLen + ciphertext.Length];
        nonce.CopyTo(result, 0);
        tag.CopyTo(result, NonceLen);
        ciphertext.CopyTo(result, NonceLen + TagLen);
        return result;
    }

    public static byte[] Decrypt(byte[] data)
    {
        if (data.Length < NonceLen + TagLen)
            throw new CryptographicException("Module data is too short.");

        var nonce      = data.AsSpan(0, NonceLen).ToArray();
        var tag        = data.AsSpan(NonceLen, TagLen).ToArray();
        var ciphertext = data.AsSpan(NonceLen + TagLen).ToArray();
        var plaintext  = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, TagLen);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }
}
