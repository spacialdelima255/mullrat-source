using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.General
{
    public static class HwidGenerator
    {
        private static string _hwid;

        private static readonly object _lock = new object();

        public static string GetHwid()
        {
            if (_hwid != null)
            {
                return _hwid;
            }
            lock (_lock)
            {
                if (_hwid != null)
                {
                    return _hwid;
                }
                List<string> list = new List<string>();
                string mg = null;
                string cpuName = null;
                List<string> vols = null;
                List<string> macs = null;
                Task task = Task.Run(delegate
                {
                    mg = GetMachineGuid();
                });
                Task task2 = Task.Run(delegate
                {
                    cpuName = GetCpuName();
                });
                Task task3 = Task.Run(delegate
                {
                    vols = GetFixedVolumeSerials();
                });
                Task task4 = Task.Run(delegate
                {
                    macs = GetMacAddresses();
                });
                Task.WaitAll(task, task2, task3, task4);
                if (!string.IsNullOrEmpty(mg))
                {
                    list.Add("MG:" + mg);
                }
                if (!string.IsNullOrEmpty(cpuName))
                {
                    list.Add("CPU:" + cpuName);
                }
                list.Add("Cores:" + Environment.ProcessorCount);
                if (vols != null && vols.Count > 0)
                {
                    list.Add("VOLS:" + string.Join(",", vols));
                }
                if (macs != null && macs.Count > 0)
                {
                    list.Add("MACS:" + string.Join(",", macs));
                }
                list.Add("MN:" + Environment.MachineName);
                _hwid = ComputeSha256(string.Join("|", list));
                return _hwid;
            }
        }

        private static string ComputeSha256(string input)
        {
            using (SHA256 sHA = SHA256.Create())
            {
                byte[] array = sHA.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder stringBuilder = new StringBuilder(array.Length * 2);
                byte[] array2 = array;
                foreach (byte b in array2)
                {
                    stringBuilder.Append(b.ToString("x2"));
                }
                return stringBuilder.ToString();
            }
        }

        private static string GetMachineGuid()
        {
            try
            {
                using (RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey("SOFTWARE\\Microsoft\\Cryptography"))
                {
                    string text = registryKey?.GetValue("MachineGuid") as string;
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text.Trim();
                    }
                }
                using (RegistryKey registryKey2 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey("SOFTWARE\\Microsoft\\Cryptography"))
                {
                    return (registryKey2?.GetValue("MachineGuid") as string)?.Trim() ?? "";
                }
            }
            catch
            {
                return "";
            }
        }

        private static string GetCpuName()
        {
            try
            {
                using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\0"))
                {
                    string text = registryKey?.GetValue("ProcessorNameString") as string;
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text.Trim();
                    }
                    return (registryKey?.GetValue("VendorIdentifier") as string)?.Trim() ?? "";
                }
            }
            catch
            {
                return "";
            }
        }

        private static List<string> GetFixedVolumeSerials()
        {
            List<string> list = new List<string>();
            try
            {
                DriveInfo[] drives = DriveInfo.GetDrives();
                foreach (DriveInfo driveInfo in drives)
                {
                    if (driveInfo.DriveType == DriveType.Fixed && driveInfo.IsReady)
                    {
                        StringBuilder stringBuilder = new StringBuilder(261);
                        StringBuilder stringBuilder2 = new StringBuilder(261);
                        if (NativeMethods.GetVolumeInformation(driveInfo.RootDirectory.FullName, stringBuilder, stringBuilder.Capacity, out var lpVolumeSerialNumber, out var _, out var _, stringBuilder2, stringBuilder2.Capacity))
                        {
                            list.Add(lpVolumeSerialNumber.ToString("X8").ToLowerInvariant());
                        }
                    }
                }
            }
            catch
            {
            }
            return list;
        }

        private static List<string> GetMacAddresses()
        {
            List<string> list = new List<string>();
            try
            {
                NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface networkInterface in allNetworkInterfaces)
                {
                    if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    {
                        continue;
                    }
                    byte[] addressBytes = networkInterface.GetPhysicalAddress().GetAddressBytes();
                    if (addressBytes.Length == 0)
                    {
                        continue;
                    }
                    StringBuilder stringBuilder = new StringBuilder();
                    for (int j = 0; j < addressBytes.Length; j++)
                    {
                        if (j > 0)
                        {
                            stringBuilder.Append(':');
                        }
                        stringBuilder.Append(addressBytes[j].ToString("x2"));
                    }
                    list.Add(stringBuilder.ToString());
                }
            }
            catch
            {
            }
            return list;
        }
    }
}
