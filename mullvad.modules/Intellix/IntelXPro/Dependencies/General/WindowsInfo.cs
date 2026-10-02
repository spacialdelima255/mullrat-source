using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.General
{
    public static class WindowsInfo
    {
        public static string GetProductName()
        {
            try
            {
                using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion"))
                {
                    if (registryKey == null)
                    {
                        return "Unknown";
                    }
                    string obj = (registryKey.GetValue("ProductName") as string) ?? "Unknown";
                    string text = (registryKey.GetValue("ReleaseId") as string) ?? (registryKey.GetValue("DisplayVersion") as string) ?? "";
                    return (obj + " " + text).Trim();
                }
            }
            catch
            {
                return "Unknown";
            }
        }

        public static string GetBuildNumber()
        {
            try
            {
                using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion"))
                {
                    if (registryKey == null)
                    {
                        return "Unknown";
                    }
                    return registryKey.GetValue("CurrentBuild")?.ToString() ?? registryKey.GetValue("CurrentBuildNumber")?.ToString() ?? "Unknown";
                }
            }
            catch
            {
                return "Unknown";
            }
        }

        public static string GetArchitecture()
        {
            try
            {
                return Environment.Is64BitOperatingSystem ? "x64" : "x86";
            }
            catch
            {
                return "Unknown";
            }
        }

        public static string GetVersion()
        {
            try
            {
                using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion"))
                {
                    if (registryKey == null)
                    {
                        return "Unknown";
                    }
                    object value = registryKey.GetValue("CurrentMajorVersionNumber");
                    object value2 = registryKey.GetValue("CurrentMinorVersionNumber");
                    if (value != null && value2 != null)
                    {
                        return $"{value}.{value2}";
                    }
                    string text = registryKey.GetValue("CurrentVersion") as string;
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text;
                    }
                    return "Unknown";
                }
            }
            catch
            {
                return "Unknown";
            }
        }

        public static string GetFullInfo()
        {
            return "OS Product: " + GetProductName() + "\nOS Build: " + GetBuildNumber() + "\nOS Arch: " + GetArchitecture();
        }
    }
}
