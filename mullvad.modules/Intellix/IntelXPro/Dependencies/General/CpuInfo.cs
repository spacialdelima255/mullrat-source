using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.General
{
    public static class CpuInfo
    {
        public static string GetName()
        {
            try
            {
                using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("HARDWARE\\DESCRIPTION\\System\\CentralProcessor\\0"))
                {
                    return (registryKey?.GetValue("ProcessorNameString") as string) ?? (registryKey?.GetValue("VendorIdentifier") as string) ?? "Unknown";
                }
            }
            catch
            {
                return "Unknown";
            }
        }

        public static int GetLogicalCores()
        {
            try
            {
                return Environment.ProcessorCount;
            }
            catch
            {
                return 0;
            }
        }
    }
}
