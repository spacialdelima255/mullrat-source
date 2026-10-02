using IntelXPro.src.IntelXPro.Dependencies.General;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.Encryption
{
    public class DpApi
    {
        private static NativeMethods.CryptprotectPromptstruct Prompt = new NativeMethods.CryptprotectPromptstruct
        {
            cbSize = Marshal.SizeOf(typeof(NativeMethods.CryptprotectPromptstruct)),
            dwPromptFlags = 0,
            hwndApp = IntPtr.Zero,
            szPrompt = null
        };

        public static byte[] Decrypt(byte[] bCipher)
        {
            NativeMethods.DataBlob pDataIn = default(NativeMethods.DataBlob);
            NativeMethods.DataBlob pDataOut = default(NativeMethods.DataBlob);
            NativeMethods.DataBlob pOptionalEntropy = default(NativeMethods.DataBlob);
            string ppszDataDescr = string.Empty;
            GCHandle gCHandle = GCHandle.Alloc(bCipher, GCHandleType.Pinned);
            pDataIn.cbData = bCipher.Length;
            pDataIn.pbData = gCHandle.AddrOfPinnedObject();
            try
            {
                if (!NativeMethods.CryptUnprotectData(ref pDataIn, ref ppszDataDescr, ref pOptionalEntropy, IntPtr.Zero, ref Prompt, 0, ref pDataOut) || pDataOut.cbData == 0)
                {
                    return null;
                }
                byte[] array = new byte[pDataOut.cbData];
                Marshal.Copy(pDataOut.pbData, array, 0, pDataOut.cbData);
                return array;
            }
            finally
            {
                gCHandle.Free();
                if (pDataOut.pbData != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pDataOut.pbData);
                }
                if (pOptionalEntropy.pbData != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pOptionalEntropy.pbData);
                }
            }
        }
    }
}
