using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using Microsoft.Win32;

namespace mullvad.Module.NetworkInformation
{
    public sealed class NetworkInformationClientModule
    {
        public static string ModuleId => "mullvad.netinfo";

        public string Execute(string action, string payload)
        {
            try
            {
                switch (action)
                {
                    case "collect": return Collect();
                    default:        return Err("Unknown action: " + action);
                }
            }
            catch (Exception ex) { return Err(ex.Message); }
        }

        private static string Collect()
        {
            var sb = new StringBuilder("[");
            bool first = true;

            void Add(string adapter, string item, string value)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"adapter\":").Append(Json(adapter))
                  .Append(",\"item\":").Append(Json(item))
                  .Append(",\"value\":").Append(Json(value))
                  .Append("}");
            }

            // General host info
            try { Add("General", "Host Name", Dns.GetHostName()); } catch { Add("General", "Host Name", "N/A"); }
            try
            {
                var domain = RegStr(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "Domain")
                          ?? RegStr(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "DhcpDomain")
                          ?? "";
                Add("General", "Domain", string.IsNullOrEmpty(domain) ? "(none)" : domain);
            }
            catch { Add("General", "Domain", "N/A"); }
            Add("General", "Public IP", "N/A (collect from server)");

            // Per-adapter details
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in interfaces)
                {
                    try
                    {
                        var name = nic.Name;
                        Add(name, "Description", nic.Description);
                        Add(name, "Type",        nic.NetworkInterfaceType.ToString());
                        Add(name, "Status",      nic.OperationalStatus.ToString());
                        Add(name, "Speed",       nic.Speed > 0 ? $"{nic.Speed / 1_000_000} Mbps" : "N/A");

                        try
                        {
                            var mac = nic.GetPhysicalAddress().ToString();
                            if (!string.IsNullOrEmpty(mac) && mac != "000000000000")
                            {
                                // Format as XX:XX:XX:XX:XX:XX
                                var formatted = new StringBuilder();
                                for (int i = 0; i < mac.Length; i += 2)
                                {
                                    if (i > 0) formatted.Append(':');
                                    formatted.Append(mac.Substring(i, Math.Min(2, mac.Length - i)));
                                }
                                Add(name, "MAC Address", formatted.ToString());
                            }
                        }
                        catch { }

                        try
                        {
                            var props = nic.GetIPProperties();

                            // Unicast addresses
                            foreach (var uni in props.UnicastAddresses)
                            {
                                try
                                {
                                    var addr = uni.Address.ToString();
                                    if (uni.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                    {
                                        Add(name, "IP Address",   addr);
                                        Add(name, "Subnet Mask",  uni.IPv4Mask?.ToString() ?? "N/A");
                                    }
                                    else if (uni.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                                    {
                                        Add(name, "IPv6 Address", addr);
                                    }
                                }
                                catch { }
                            }

                            // Gateway
                            foreach (var gw in props.GatewayAddresses)
                            {
                                try { Add(name, "Default Gateway", gw.Address.ToString()); } catch { }
                            }

                            // DNS servers
                            foreach (var dns in props.DnsAddresses)
                            {
                                try { Add(name, "DNS Server", dns.ToString()); } catch { }
                            }

                            // DHCP
                            try
                            {
                                Add(name, "DHCP Enabled",  props.GetIPv4Properties()?.IsDhcpEnabled == true ? "Yes" : "No");
                                var dhcpServer = props.DhcpServerAddresses;
                                if (dhcpServer.Count > 0)
                                    Add(name, "DHCP Server", dhcpServer[0].ToString());
                            }
                            catch { }
                        }
                        catch { }
                    }
                    catch { }
                }
            }
            catch { Add("Error", "Enumeration failed", "Unable to enumerate network interfaces"); }

            sb.Append("]");
            return sb.ToString();
        }

        private static string? RegStr(string keyPath, string valueName)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyPath, false);
                return key?.GetValue(valueName)?.ToString();
            }
            catch { return null; }
        }

        private static string Err(string msg)
            => "{\"success\":false,\"error\":\"" + msg.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ") + "\"}";

        private static string Json(string s)
        {
            if (s == null) return "null";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                           .Replace("\r", "\\r").Replace("\n", "\\n")
                           .Replace("\t", "\\t") + "\"";
        }
    }
}
