using System;
using System.Runtime.InteropServices;
using mullvad.Enums;

namespace mullvad.Helpers
{
    public static class PlatformHelper
    {
        public static PlatformArch Arch
        {
            get
            {
                switch (RuntimeInformation.ProcessArchitecture)
                {
                    case Architecture.X86:   return PlatformArch.X86;
                    case Architecture.X64:   return PlatformArch.X64;
                    case Architecture.Arm64: return PlatformArch.Arm64;
                    default:                 return PlatformArch.Unknown;
                }
            }
        }

        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsLinux   => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public static bool IsMacOS   => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static string OsDescription => RuntimeInformation.OSDescription;

        public static string Hostname
        {
            get
            {
                try { return System.Net.Dns.GetHostName(); }
                catch { return Environment.MachineName; }
            }
        }

        public static string Username => Environment.UserName;

        public static string DotNetRuntime => RuntimeInformation.FrameworkDescription;

        public static bool Is64BitProcess => IntPtr.Size == 8;
    }
}
