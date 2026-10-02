using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.Encryption
{
    public static class YaAuthenticatedData
    {
        public static byte[] Decrypt(byte[] encryptionKey, byte[] password_value, string url, string username_element, string password_element, string username_value, string signon_realm)
        {
            byte[] aad = new byte[0];
            using (SHA1 sHA = SHA1.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(url);
                byte[] bytes2 = Encoding.UTF8.GetBytes(username_element);
                byte[] bytes3 = Encoding.UTF8.GetBytes(username_value);
                byte[] bytes4 = Encoding.UTF8.GetBytes(password_element);
                byte[] bytes5 = Encoding.UTF8.GetBytes(signon_realm);
                byte[] array = new byte[bytes.Length + 1 + bytes2.Length + 1 + bytes3.Length + 1 + bytes4.Length + 1 + bytes5.Length];
                int num = 0;
                Array.Copy(bytes, 0, array, num, bytes.Length);
                num += bytes.Length;
                array[num++] = 0;
                Array.Copy(bytes2, 0, array, num, bytes2.Length);
                num += bytes2.Length;
                array[num++] = 0;
                Array.Copy(bytes3, 0, array, num, bytes3.Length);
                num += bytes3.Length;
                array[num++] = 0;
                Array.Copy(bytes4, 0, array, num, bytes4.Length);
                num += bytes4.Length;
                array[num++] = 0;
                Array.Copy(bytes5, 0, array, num, bytes5.Length);
                aad = sHA.ComputeHash(array);
            }
            byte[] array2 = new byte[12];
            Array.Copy(password_value, 0, array2, 0, 12);
            int num2 = password_value.Length - 12 - 16;
            byte[] array3 = new byte[num2];
            Array.Copy(password_value, 12, array3, 0, num2);
            byte[] array4 = new byte[16];
            Array.Copy(password_value, password_value.Length - 16, array4, 0, 16);
            return AesGcm256.Decrypt(encryptionKey, array2, aad, array3, array4);
        }
    }
}
