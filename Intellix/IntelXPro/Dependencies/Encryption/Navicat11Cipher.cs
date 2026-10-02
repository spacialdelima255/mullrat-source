using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace IntelXPro.src.IntelXPro.Dependencies.Encryption
{
    public class Navicat11Cipher
    {
        private Blowfish blowfishCipher;

        protected byte[] StringToByteArray(string hex)
        {
            return (from x in Enumerable.Range(0, hex.Length)
                    where x % 2 == 0
                    select Convert.ToByte(hex.Substring(x, 2), 16)).ToArray();
        }

        protected void XorBytes(byte[] a, byte[] b, int len)
        {
            for (int i = 0; i < len; i++)
            {
                a[i] ^= b[i];
            }
        }

        public Navicat11Cipher()
        {
            byte[] array = new byte[8] { 51, 68, 67, 53, 67, 65, 51, 57 };
            using (SHA1CryptoServiceProvider sHA1CryptoServiceProvider = new SHA1CryptoServiceProvider())
            {
                sHA1CryptoServiceProvider.TransformFinalBlock(array, 0, array.Length);
                blowfishCipher = new Blowfish(sHA1CryptoServiceProvider.Hash);
            }
        }

        public string DecryptString(string ciphertext)
        {
            byte[] array = StringToByteArray(ciphertext);
            byte[] array2 = Enumerable.Repeat(byte.MaxValue, blowfishCipher.BlockSize).ToArray();
            blowfishCipher.Encrypt(array2, Blowfish.Endian.Big);
            byte[] array3 = new byte[0];
            int num = array.Length / blowfishCipher.BlockSize;
            int num2 = array.Length % blowfishCipher.BlockSize;
            byte[] array4 = new byte[blowfishCipher.BlockSize];
            byte[] array5 = new byte[blowfishCipher.BlockSize];
            for (int i = 0; i < num; i++)
            {
                Array.Copy(array, blowfishCipher.BlockSize * i, array4, 0, blowfishCipher.BlockSize);
                Array.Copy(array4, array5, blowfishCipher.BlockSize);
                blowfishCipher.Decrypt(array4, Blowfish.Endian.Big);
                XorBytes(array4, array2, blowfishCipher.BlockSize);
                array3 = array3.Concat(array4).ToArray();
                XorBytes(array2, array5, blowfishCipher.BlockSize);
            }
            if (num2 != 0)
            {
                Array.Clear(array4, 0, array4.Length);
                Array.Copy(array, blowfishCipher.BlockSize * num, array4, 0, num2);
                blowfishCipher.Encrypt(array2, Blowfish.Endian.Big);
                XorBytes(array4, array2, blowfishCipher.BlockSize);
                array3 = array3.Concat(array4.Take(num2).ToArray()).ToArray();
            }
            return Encoding.UTF8.GetString(array3);
        }
    }
}