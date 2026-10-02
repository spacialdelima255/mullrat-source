using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.Encryption
{
    public class RC4Crypt
    {
        public static byte[] Decrypt(byte[] key, byte[] data)
        {
            if (key == null)
            {
                return null;
            }
            if (key.Length == 0)
            {
                return null;
            }
            if (data == null)
            {
                return null;
            }
            byte[] array = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                array[i] = (byte)i;
            }
            int num = 0;
            for (int j = 0; j < 256; j++)
            {
                num = (num + array[j] + key[j % key.Length]) & 0xFF;
                Swap(array, j, num);
            }
            byte[] array2 = new byte[data.Length];
            int num2 = 0;
            num = 0;
            for (int k = 0; k < data.Length; k++)
            {
                num2 = (num2 + 1) & 0xFF;
                num = (num + array[num2]) & 0xFF;
                Swap(array, num2, num);
                byte b = array[(array[num2] + array[num]) & 0xFF];
                array2[k] = (byte)(data[k] ^ b);
            }
            return array2;
        }

        private static void Swap(byte[] arr, int a, int b)
        {
            byte b2 = arr[a];
            arr[a] = arr[b];
            arr[b] = b2;
        }
    }
}
