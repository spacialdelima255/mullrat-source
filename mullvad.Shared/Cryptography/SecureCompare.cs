using System.Runtime.CompilerServices;

namespace mullvad.Cryptography
{
    /// <summary>
    /// Constant-time equality checks. No early exit on mismatch — timing side-channels
    /// cannot distinguish a wrong first byte from a wrong last byte.
    /// </summary>
    public static class SecureCompare
    {
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static bool Equals(byte[] a, byte[] b)
        {
            if (a is null || b is null) return a is null && b is null;
            if (a.Length != b.Length)   return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static bool Equals(string a, string b)
        {
            if (a is null || b is null) return a is null && b is null;
            if (a.Length != b.Length)   return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
