using IntelXPro.src.IntelXPro.Dependencies.Sql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.Encryption
{
    public static class LocalEncryptor
    {
        public static byte[] ExtractEncryptionKey(SqLite sql, byte[] encryptionKey)
        {
            byte[] array = new byte[0];
            if (sql.ReadTable("meta"))
            {
                for (int i = 0; i < sql.GetRowCount(); i++)
                {
                    if (sql.GetValue(i, 0).Equals("local_encryptor_data"))
                    {
                        array = Encoding.Default.GetBytes(sql.GetValue(i, 1));
                        break;
                    }
                }
            }
            int num = FindByteSequence(array, Encoding.ASCII.GetBytes("v10"));
            if (num == -1)
            {
                return null;
            }
            byte[] array2 = new byte[96];
            Array.Copy(array, num + 3, array2, 0, 96);
            byte[] array3 = new byte[12];
            Array.Copy(array2, 0, array3, 0, 12);
            int num2 = array2.Length - 12 - 16;
            byte[] array4 = new byte[num2];
            Array.Copy(array2, 12, array4, 0, num2);
            byte[] array5 = new byte[16];
            Array.Copy(array2, array2.Length - 16, array5, 0, 16);
            byte[] array6 = AesGcm256.Decrypt(encryptionKey, array3, null, array4, array5);
            if (BitConverter.ToInt32(array6, 0) == 538050824)
            {
                byte[] array7 = new byte[32];
                Array.Copy(array6, 4, array7, 0, 32);
                return array7;
            }
            return null;
        }

        private static int FindByteSequence(byte[] src, byte[] pattern)
        {
            int num = src.Length - pattern.Length + 1;
            for (int i = 0; i < num; i++)
            {
                if (src[i] != pattern[0])
                {
                    continue;
                }
                int num2 = pattern.Length - 1;
                while (num2 >= 1 && src[i + num2] == pattern[num2])
                {
                    if (num2 == 1)
                    {
                        return i;
                    }
                    num2--;
                }
            }
            return -1;
        }
    }
}
