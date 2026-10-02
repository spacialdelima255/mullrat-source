using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using IntelXPro.src.IntelXPro.Dependencies.General;

namespace IntelXPro.src.IntelXPro.Dependencies.Encryption
{
    public static class CngDecryptor
    {
        private const int NCRYPT_SILENT_FLAG = 64;

        public static byte[] Decrypt(byte[] inputData, string providerName = "Microsoft Software Key Storage Provider", string keyName = "Google Chromekey1")
        {
            IntPtr phProvider = IntPtr.Zero;
            IntPtr phKey = IntPtr.Zero;
            try
            {
                int num = NativeMethods.NCryptOpenStorageProvider(out phProvider, providerName, 0);
                if (num != 0)
                {
                    throw new Exception($"NCryptOpenStorageProvider error: Code {num}");
                }
                num = NativeMethods.NCryptOpenKey(phProvider, out phKey, keyName, 0, 0);
                if (num != 0)
                {
                    throw new Exception($"NCryptOpenKey error: Code {num}");
                }
                num = NativeMethods.NCryptDecrypt(phKey, inputData, inputData.Length, IntPtr.Zero, null, 0, out var pcbResult, 64);
                if (num != 0)
                {
                    throw new Exception($"NCryptDecrypt size determination error: Code {num}");
                }
                byte[] array = new byte[pcbResult];
                num = NativeMethods.NCryptDecrypt(phKey, inputData, inputData.Length, IntPtr.Zero, array, array.Length, out pcbResult, 64);
                if (num != 0)
                {
                    throw new Exception($"NCryptDecrypt error: Code {num}");
                }
                Array.Resize(ref array, pcbResult);
                return array;
            }
            finally
            {
                if (phKey != IntPtr.Zero)
                {
                    NativeMethods.NCryptFreeObject(phKey);
                }
                if (phProvider != IntPtr.Zero)
                {
                    NativeMethods.NCryptFreeObject(phProvider);
                }
            }
        }
    }
}
